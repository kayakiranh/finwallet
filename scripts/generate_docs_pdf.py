#!/usr/bin/env python3
"""Generate Turkish and English PDF documentation from bilingual Markdown files."""

from __future__ import annotations

import html
import re
import shutil
import textwrap
from pathlib import Path

from pypdf import PdfWriter
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    ListFlowable,
    ListItem,
    PageBreak,
    Paragraph,
    Preformatted,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
OUT = DOCS / "pdf"
TR_OUT = OUT / "TR"
EN_OUT = OUT / "EN"

BODY_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
MONO_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"


def register_fonts() -> None:
    """Register fonts that support Turkish and common Unicode glyphs."""
    pdfmetrics.registerFont(TTFont("DejaVuSans", BODY_FONT))
    pdfmetrics.registerFont(TTFont("DejaVuSans-Bold", BOLD_FONT))
    pdfmetrics.registerFont(TTFont("DejaVuSansMono", MONO_FONT))


def source_documents() -> list[Path]:
    """Return numbered maintained project documents 00 through 19."""
    docs: list[Path] = []
    for path in DOCS.glob("[0-1][0-9]-*.md"):
        try:
            number = int(path.name[:2])
        except ValueError:
            continue
        if 0 <= number <= 19:
            docs.append(path)
    return sorted(docs)


def split_document(text: str) -> tuple[str, str, str, str]:
    """Split a bilingual Markdown document into Turkish and English bodies."""
    title = text.splitlines()[0].lstrip("# ").strip()
    tr_marker = "## Türkçe"
    en_marker = "## English"
    tr_pos = text.find(tr_marker)
    en_pos = text.find(en_marker)
    if tr_pos < 0 or en_pos < 0 or en_pos <= tr_pos:
        raise ValueError("Document must contain '## Türkçe' followed by '## English'.")

    tr_body = text[tr_pos + len(tr_marker):en_pos].strip()
    en_body = text[en_pos + len(en_marker):].strip()

    if " / " in title:
        tr_title, en_title = title.split(" / ", 1)
    else:
        tr_title = en_title = title
    return tr_title.strip(), tr_body, en_title.strip(), en_body


def inline_markup(value: str) -> str:
    """Convert a small safe subset of inline Markdown into ReportLab markup."""
    escaped = html.escape(value, quote=False)
    escaped = re.sub(r"`([^`]+)`", r'<font name="DejaVuSansMono">\1</font>', escaped)
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(r"\*([^*]+)\*", r"<i>\1</i>", escaped)
    return escaped


def styles() -> dict[str, ParagraphStyle]:
    """Build deterministic styles for generated documentation PDFs."""
    sample = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "DocTitle", parent=sample["Title"], fontName="DejaVuSans-Bold",
            fontSize=20, leading=25, spaceAfter=10 * mm, alignment=TA_LEFT,
        ),
        "h2": ParagraphStyle(
            "H2", parent=sample["Heading2"], fontName="DejaVuSans-Bold",
            fontSize=14, leading=18, spaceBefore=5 * mm, spaceAfter=2 * mm,
        ),
        "h3": ParagraphStyle(
            "H3", parent=sample["Heading3"], fontName="DejaVuSans-Bold",
            fontSize=11.5, leading=15, spaceBefore=4 * mm, spaceAfter=1.5 * mm,
        ),
        "body": ParagraphStyle(
            "Body", parent=sample["BodyText"], fontName="DejaVuSans",
            fontSize=9.3, leading=13.2, spaceAfter=2.2 * mm,
        ),
        "bullet": ParagraphStyle(
            "Bullet", parent=sample["BodyText"], fontName="DejaVuSans",
            fontSize=9, leading=12.5, leftIndent=2 * mm,
        ),
        "table": ParagraphStyle(
            "TableCell", parent=sample["BodyText"], fontName="DejaVuSans",
            fontSize=7.7, leading=10,
        ),
        "quote": ParagraphStyle(
            "Quote", parent=sample["BodyText"], fontName="DejaVuSans",
            fontSize=8.8, leading=12.5, leftIndent=6 * mm, rightIndent=4 * mm,
            borderPadding=4, borderColor=colors.HexColor("#BBBBBB"), borderWidth=0.5,
        ),
    }


def code_block(lines: list[str]) -> Preformatted:
    """Create a wrapped monospace code block that fits on A4."""
    wrapped: list[str] = []
    for line in lines:
        if len(line) <= 94:
            wrapped.append(line)
        else:
            parts = textwrap.wrap(
                line, width=94, replace_whitespace=False, drop_whitespace=False,
                subsequent_indent="    ", break_long_words=True, break_on_hyphens=False,
            )
            wrapped.extend(parts or [line])
    return Preformatted(
        "\n".join(wrapped),
        ParagraphStyle(
            "Code", fontName="DejaVuSansMono", fontSize=7.4, leading=10,
            leftIndent=3 * mm, rightIndent=3 * mm, spaceBefore=2 * mm,
            spaceAfter=3 * mm, backColor=colors.HexColor("#F4F4F4"),
            borderColor=colors.HexColor("#D8D8D8"), borderWidth=0.5,
            borderPadding=5,
        ),
    )


def markdown_story(body: str, doc_styles: dict[str, ParagraphStyle]) -> list:
    """Render the Markdown subset used by FinWallet documentation into flowables."""
    lines = body.splitlines()
    story: list = []
    paragraph: list[str] = []
    i = 0

    def flush_paragraph() -> None:
        if paragraph:
            text = " ".join(item.strip() for item in paragraph).strip()
            if text and text != "---":
                story.append(Paragraph(inline_markup(text), doc_styles["body"]))
            paragraph.clear()

    while i < len(lines):
        raw = lines[i]
        line = raw.rstrip()
        stripped = line.strip()

        if stripped.startswith("```"):
            flush_paragraph()
            i += 1
            code: list[str] = []
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code.append(lines[i].rstrip("\n"))
                i += 1
            story.append(code_block(code))
            i += 1
            continue

        if stripped.startswith("|") and stripped.endswith("|"):
            flush_paragraph()
            table_lines: list[str] = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i].strip())
                i += 1
            rows: list[list[str]] = []
            for row_idx, table_line in enumerate(table_lines):
                cells = [cell.strip() for cell in table_line.strip("|").split("|")]
                if row_idx == 1 and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
                    continue
                rows.append(cells)
            if rows:
                cols = max(len(row) for row in rows)
                width = (A4[0] - 34 * mm) / max(cols, 1)
                data = [
                    [Paragraph(inline_markup(cell), doc_styles["table"]) for cell in row]
                    for row in rows
                ]
                table = Table(data, colWidths=[width] * cols, repeatRows=1, hAlign="LEFT")
                table.setStyle(TableStyle([
                    ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#ECEFF1")),
                    ("FONTNAME", (0, 0), (-1, 0), "DejaVuSans-Bold"),
                    ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#C5C5C5")),
                    ("VALIGN", (0, 0), (-1, -1), "TOP"),
                    ("LEFTPADDING", (0, 0), (-1, -1), 4),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                    ("TOPPADDING", (0, 0), (-1, -1), 4),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
                ]))
                story.extend([table, Spacer(1, 3 * mm)])
            continue

        heading = re.match(r"^(#{2,4})\s+(.+)$", stripped)
        if heading:
            flush_paragraph()
            level = len(heading.group(1))
            style = doc_styles["h2"] if level == 2 else doc_styles["h3"]
            story.append(Paragraph(inline_markup(heading.group(2)), style))
            i += 1
            continue

        if re.match(r"^[-*]\s+", stripped):
            flush_paragraph()
            items: list[ListItem] = []
            while i < len(lines) and re.match(r"^[-*]\s+", lines[i].strip()):
                item = re.sub(r"^[-*]\s+", "", lines[i].strip())
                items.append(ListItem(Paragraph(inline_markup(item), doc_styles["bullet"])))
                i += 1
            story.append(ListFlowable(items, bulletType="bullet", leftIndent=8 * mm, bulletFontName="DejaVuSans"))
            story.append(Spacer(1, 2 * mm))
            continue

        if re.match(r"^\d+\.\s+", stripped):
            flush_paragraph()
            items = []
            while i < len(lines) and re.match(r"^\d+\.\s+", lines[i].strip()):
                item = re.sub(r"^\d+\.\s+", "", lines[i].strip())
                items.append(ListItem(Paragraph(inline_markup(item), doc_styles["bullet"])))
                i += 1
            story.append(ListFlowable(items, bulletType="1", leftIndent=9 * mm, bulletFontName="DejaVuSans"))
            story.append(Spacer(1, 2 * mm))
            continue

        if stripped.startswith(">"):
            flush_paragraph()
            quote = stripped.lstrip("> ")
            story.append(Paragraph(inline_markup(quote), doc_styles["quote"]))
            story.append(Spacer(1, 2 * mm))
            i += 1
            continue

        if not stripped or stripped == "---":
            flush_paragraph()
            i += 1
            continue

        paragraph.append(stripped)
        i += 1

    flush_paragraph()
    return story


def build_pdf(title: str, body: str, output: Path) -> None:
    """Build one language-specific documentation PDF."""
    output.parent.mkdir(parents=True, exist_ok=True)
    doc_styles = styles()
    story = [Paragraph(inline_markup(title), doc_styles["title"])]
    story.extend(markdown_story(body, doc_styles))

    document = SimpleDocTemplate(
        str(output), pagesize=A4,
        rightMargin=17 * mm, leftMargin=17 * mm,
        topMargin=16 * mm, bottomMargin=16 * mm,
        title=title, author="FinWallet",
    )
    document.build(story)


def merge_pdfs(paths: list[Path], output: Path) -> None:
    """Merge language-specific PDFs in document-number order."""
    writer = PdfWriter()
    for path in paths:
        writer.append(str(path))
    with output.open("wb") as stream:
        writer.write(stream)
    writer.close()


def main() -> None:
    """Generate all individual and combined Turkish/English PDFs."""
    register_fonts()
    if OUT.exists():
        shutil.rmtree(OUT)
    TR_OUT.mkdir(parents=True)
    EN_OUT.mkdir(parents=True)

    tr_paths: list[Path] = []
    en_paths: list[Path] = []

    documents = source_documents()
    if len(documents) != 20:
        raise RuntimeError(f"Expected 20 numbered documents (00-19), found {len(documents)}.")

    for source in documents:
        tr_title, tr_body, en_title, en_body = split_document(source.read_text(encoding="utf-8"))
        stem = source.stem
        tr_path = TR_OUT / f"TR_{stem}.pdf"
        en_path = EN_OUT / f"EN_{stem}.pdf"
        build_pdf(tr_title, tr_body, tr_path)
        build_pdf(en_title, en_body, en_path)
        tr_paths.append(tr_path)
        en_paths.append(en_path)

    merge_pdfs(tr_paths, OUT / "TR_FinWallet_Tum_Dokumanlar.pdf")
    merge_pdfs(en_paths, OUT / "EN_FinWallet_All_Documents.pdf")

    print(f"Generated {len(tr_paths)} TR + {len(en_paths)} EN PDFs under {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

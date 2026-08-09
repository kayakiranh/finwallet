#!/usr/bin/env python3
"""Generate TR and EN Docker runbook PDFs from docs/20-docker-runbook.md."""

from __future__ import annotations

import html
import re
import textwrap
from pathlib import Path

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
    Paragraph,
    Preformatted,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "20-docker-runbook.md"
TR_OUTPUT = ROOT / "docs" / "pdf" / "TR" / "TR_20-docker-runbook.pdf"
EN_OUTPUT = ROOT / "docs" / "pdf" / "EN" / "EN_20-docker-runbook.pdf"

BODY_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
MONO_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"


def register_fonts() -> None:
    """Register Unicode fonts so Turkish glyphs render reliably."""
    pdfmetrics.registerFont(TTFont("DejaVuSans", BODY_FONT))
    pdfmetrics.registerFont(TTFont("DejaVuSans-Bold", BOLD_FONT))
    pdfmetrics.registerFont(TTFont("DejaVuSansMono", MONO_FONT))


def safe_text(value: str) -> str:
    """Escape text for ReportLab and remove Markdown backticks."""
    return html.escape(value, quote=False).replace("`", "")


def split_document(text: str) -> tuple[str, str, str, str]:
    """Split the bilingual Markdown source into Turkish and English sections."""
    title = text.splitlines()[0].lstrip("# ").strip()
    tr_marker = "## Türkçe"
    en_marker = "## English"
    tr_pos = text.find(tr_marker)
    en_pos = text.find(en_marker)
    if tr_pos < 0 or en_pos <= tr_pos:
        raise ValueError("Document must contain Turkish and English sections.")

    tr_body = text[tr_pos + len(tr_marker):en_pos].strip()
    en_body = text[en_pos + len(en_marker):].strip()

    if " / " in title:
        tr_title, en_title = title.split(" / ", 1)
    else:
        tr_title = en_title = title

    return tr_title.strip(), tr_body, en_title.strip(), en_body


def styles() -> dict[str, ParagraphStyle]:
    """Create deterministic PDF styles."""
    sample = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "DockerRunbookTitle",
            parent=sample["Title"],
            fontName="DejaVuSans-Bold",
            fontSize=20,
            leading=25,
            spaceAfter=8 * mm,
            alignment=TA_LEFT,
        ),
        "h2": ParagraphStyle(
            "DockerRunbookH2",
            parent=sample["Heading2"],
            fontName="DejaVuSans-Bold",
            fontSize=13.5,
            leading=17,
            spaceBefore=5 * mm,
            spaceAfter=2 * mm,
        ),
        "h3": ParagraphStyle(
            "DockerRunbookH3",
            parent=sample["Heading3"],
            fontName="DejaVuSans-Bold",
            fontSize=11.2,
            leading=14.5,
            spaceBefore=4 * mm,
            spaceAfter=1.5 * mm,
        ),
        "body": ParagraphStyle(
            "DockerRunbookBody",
            parent=sample["BodyText"],
            fontName="DejaVuSans",
            fontSize=9.1,
            leading=13,
            spaceAfter=2 * mm,
        ),
        "list": ParagraphStyle(
            "DockerRunbookList",
            parent=sample["BodyText"],
            fontName="DejaVuSans",
            fontSize=8.9,
            leading=12.3,
        ),
        "table": ParagraphStyle(
            "DockerRunbookTable",
            parent=sample["BodyText"],
            fontName="DejaVuSans",
            fontSize=7.3,
            leading=9.3,
        ),
        "quote": ParagraphStyle(
            "DockerRunbookQuote",
            parent=sample["BodyText"],
            fontName="DejaVuSans",
            fontSize=8.8,
            leading=12.2,
            leftIndent=6 * mm,
            rightIndent=4 * mm,
            borderPadding=4,
            borderColor=colors.HexColor("#BBBBBB"),
            borderWidth=0.5,
        ),
    }


def make_code(lines: list[str]) -> Preformatted:
    """Render code blocks with conservative line wrapping."""
    wrapped: list[str] = []
    for line in lines:
        if len(line) <= 92:
            wrapped.append(line)
            continue
        parts = textwrap.wrap(
            line,
            width=92,
            replace_whitespace=False,
            drop_whitespace=False,
            subsequent_indent="    ",
            break_long_words=True,
            break_on_hyphens=False,
        )
        wrapped.extend(parts or [line])

    return Preformatted(
        "\n".join(wrapped),
        ParagraphStyle(
            "DockerRunbookCode",
            fontName="DejaVuSansMono",
            fontSize=7.1,
            leading=9.5,
            leftIndent=3 * mm,
            rightIndent=3 * mm,
            spaceBefore=2 * mm,
            spaceAfter=3 * mm,
            backColor=colors.HexColor("#F4F4F4"),
            borderColor=colors.HexColor("#D8D8D8"),
            borderWidth=0.5,
            borderPadding=5,
        ),
    )


def render_table(table_lines: list[str], style: ParagraphStyle) -> Table | None:
    """Render a simple Markdown table."""
    rows: list[list[str]] = []
    for index, line in enumerate(table_lines):
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if index == 1 and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
            continue
        rows.append(cells)

    if not rows:
        return None

    columns = max(len(row) for row in rows)
    for row in rows:
        row.extend([""] * (columns - len(row)))

    available_width = A4[0] - 34 * mm
    column_width = available_width / max(columns, 1)
    data = [[Paragraph(safe_text(cell), style) for cell in row] for row in rows]
    table = Table(data, colWidths=[column_width] * columns, repeatRows=1, hAlign="LEFT")
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
    return table


def markdown_story(body: str, doc_styles: dict[str, ParagraphStyle]) -> list:
    """Convert the runbook Markdown subset into ReportLab flowables."""
    lines = body.splitlines()
    story: list = []
    paragraph: list[str] = []
    index = 0

    def flush_paragraph() -> None:
        if not paragraph:
            return
        text = " ".join(part.strip() for part in paragraph).strip()
        if text and text != "---":
            story.append(Paragraph(safe_text(text), doc_styles["body"]))
        paragraph.clear()

    while index < len(lines):
        stripped = lines[index].strip()

        if stripped.startswith("```"):
            flush_paragraph()
            index += 1
            code: list[str] = []
            while index < len(lines) and not lines[index].strip().startswith("```"):
                code.append(lines[index].rstrip())
                index += 1
            story.append(make_code(code))
            index += 1
            continue

        if stripped.startswith("|") and stripped.endswith("|"):
            flush_paragraph()
            table_lines: list[str] = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                table_lines.append(lines[index].strip())
                index += 1
            table = render_table(table_lines, doc_styles["table"])
            if table is not None:
                story.extend([table, Spacer(1, 3 * mm)])
            continue

        heading = re.match(r"^(#{2,4})\s+(.+)$", stripped)
        if heading:
            flush_paragraph()
            heading_style = doc_styles["h2"] if len(heading.group(1)) == 2 else doc_styles["h3"]
            story.append(Paragraph(safe_text(heading.group(2)), heading_style))
            index += 1
            continue

        if re.match(r"^[-*]\s+", stripped):
            flush_paragraph()
            items: list[ListItem] = []
            while index < len(lines) and re.match(r"^[-*]\s+", lines[index].strip()):
                item = re.sub(r"^[-*]\s+", "", lines[index].strip())
                items.append(ListItem(Paragraph(safe_text(item), doc_styles["list"])))
                index += 1
            story.append(ListFlowable(items, bulletType="bullet", leftIndent=8 * mm, bulletFontName="DejaVuSans"))
            story.append(Spacer(1, 2 * mm))
            continue

        if re.match(r"^\d+\.\s+", stripped):
            flush_paragraph()
            items: list[ListItem] = []
            while index < len(lines) and re.match(r"^\d+\.\s+", lines[index].strip()):
                item = re.sub(r"^\d+\.\s+", "", lines[index].strip())
                items.append(ListItem(Paragraph(safe_text(item), doc_styles["list"])))
                index += 1
            story.append(ListFlowable(items, bulletType="1", leftIndent=9 * mm, bulletFontName="DejaVuSans"))
            story.append(Spacer(1, 2 * mm))
            continue

        if stripped.startswith(">"):
            flush_paragraph()
            story.append(Paragraph(safe_text(stripped.lstrip("> ")), doc_styles["quote"]))
            story.append(Spacer(1, 2 * mm))
            index += 1
            continue

        if not stripped or stripped == "---":
            flush_paragraph()
            index += 1
            continue

        paragraph.append(stripped)
        index += 1

    flush_paragraph()
    return story


def build_pdf(title: str, body: str, output: Path) -> None:
    """Create one A4 PDF."""
    output.parent.mkdir(parents=True, exist_ok=True)
    doc_styles = styles()
    story = [Paragraph(safe_text(title), doc_styles["title"])]
    story.extend(markdown_story(body, doc_styles))

    document = SimpleDocTemplate(
        str(output),
        pagesize=A4,
        rightMargin=17 * mm,
        leftMargin=17 * mm,
        topMargin=16 * mm,
        bottomMargin=16 * mm,
        title=title,
        author="FinWallet",
    )
    document.build(story)


def main() -> None:
    """Generate the two language-specific Docker runbook PDFs."""
    register_fonts()
    tr_title, tr_body, en_title, en_body = split_document(SOURCE.read_text(encoding="utf-8"))
    build_pdf(tr_title, tr_body, TR_OUTPUT)
    build_pdf(en_title, en_body, EN_OUTPUT)
    print(f"Generated {TR_OUTPUT.relative_to(ROOT)}")
    print(f"Generated {EN_OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

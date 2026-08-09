using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;
using FinWallet.Domain.Registration;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Parametreli MSSQL sorgularından dönen satırları reflection kullanmadan Customer, CustomerCredential, CustomerSession ve RefreshToken domain nesnelerine dönüştüren merkezi materializer'dır.
/// EN: Central materializer that converts rows returned by parameterized MSSQL queries into Customer, CustomerCredential, CustomerSession and RefreshToken domain objects without using reflection.
/// </summary>
internal static class SqlAuthenticationMaterializer
{
    /// <summary>
    /// TR: Reader'ın mevcut satırındaki customer kolonlarını kontrollü <see cref="Customer.Restore"/> factory'si üzerinden domain nesnesine dönüştürür.
    /// EN: Converts customer columns from the reader's current row into a domain object through the controlled <see cref="Customer.Restore"/> factory.
    /// </summary>
    /// <param name="reader">TR: Customer kolonlarını içeren açık SqlDataReader. EN: Open SqlDataReader containing customer columns.</param>
    /// <param name="prefix">TR: Join sorgularında kolon alias'larının başına eklenen prefix. EN: Prefix added to column aliases in join queries.</param>
    /// <returns>TR: Kalıcı state'ten yeniden oluşturulan Customer nesnesini döndürür. EN: Returns the Customer object rehydrated from persisted state.</returns>
    public static Customer ReadCustomer(SqlDataReader reader, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(reader);

        var phone = PhoneNumber.Create(reader.GetString(reader.GetOrdinal($"{prefix}PhoneNumber")));
        var emailOrdinal = reader.GetOrdinal($"{prefix}Email");
        var email = reader.IsDBNull(emailOrdinal) ? null : reader.GetString(emailOrdinal);

        return Customer.Restore(
            reader.GetGuid(reader.GetOrdinal($"{prefix}Id")),
            reader.GetString(reader.GetOrdinal($"{prefix}CountryCode")),
            phone,
            email,
            (CustomerStatus)reader.GetByte(reader.GetOrdinal($"{prefix}Status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}CreatedAt")));
    }

    /// <summary>
    /// TR: Reader'ın mevcut satırındaki credential kolonlarını kontrollü <see cref="CustomerCredential.Restore"/> factory'si üzerinden domain nesnesine dönüştürür.
    /// EN: Converts credential columns from the reader's current row into a domain object through the controlled <see cref="CustomerCredential.Restore"/> factory.
    /// </summary>
    /// <param name="reader">TR: Credential kolonlarını içeren açık SqlDataReader. EN: Open SqlDataReader containing credential columns.</param>
    /// <param name="prefix">TR: Join sorgularında credential kolon alias prefix'i. EN: Credential column-alias prefix in join queries.</param>
    /// <returns>TR: Kalıcı state'ten yeniden oluşturulan credential nesnesini döndürür. EN: Returns the credential object rehydrated from persisted state.</returns>
    public static CustomerCredential ReadCredential(SqlDataReader reader, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(reader);
        var lockedUntilOrdinal = reader.GetOrdinal($"{prefix}LockedUntil");

        return CustomerCredential.Restore(
            reader.GetGuid(reader.GetOrdinal($"{prefix}CustomerId")),
            reader.GetString(reader.GetOrdinal($"{prefix}PasswordHash")),
            reader.GetString(reader.GetOrdinal($"{prefix}PasswordSalt")),
            reader.GetInt32(reader.GetOrdinal($"{prefix}PasswordHashVersion")),
            reader.GetInt32(reader.GetOrdinal($"{prefix}FailedLoginCount")),
            reader.IsDBNull(lockedUntilOrdinal)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(lockedUntilOrdinal),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}PasswordChangedAt")));
    }

    /// <summary>
    /// TR: Reader'ın mevcut satırındaki session kolonlarını kontrollü <see cref="CustomerSession.Restore"/> factory'si üzerinden domain nesnesine dönüştürür.
    /// EN: Converts session columns from the reader's current row into a domain object through the controlled <see cref="CustomerSession.Restore"/> factory.
    /// </summary>
    /// <param name="reader">TR: Session kolonlarını içeren açık SqlDataReader. EN: Open SqlDataReader containing session columns.</param>
    /// <param name="prefix">TR: Join sorgularında session kolon alias prefix'i. EN: Session column-alias prefix in join queries.</param>
    /// <returns>TR: Kalıcı state'ten yeniden oluşturulan session nesnesini döndürür. EN: Returns the session object rehydrated from persisted state.</returns>
    public static CustomerSession ReadSession(SqlDataReader reader, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(reader);
        var revokedAtOrdinal = reader.GetOrdinal($"{prefix}RevokedAt");

        return CustomerSession.Restore(
            reader.GetGuid(reader.GetOrdinal($"{prefix}Id")),
            reader.GetGuid(reader.GetOrdinal($"{prefix}CustomerId")),
            reader.GetString(reader.GetOrdinal($"{prefix}DeviceId")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}CreatedAt")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}LastActivityAt")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}ExpiresAt")),
            reader.IsDBNull(revokedAtOrdinal)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(revokedAtOrdinal));
    }

    /// <summary>
    /// TR: Reader'ın mevcut satırındaki refresh-token kolonlarını kontrollü <see cref="RefreshToken.Restore"/> factory'si üzerinden domain nesnesine dönüştürür.
    /// EN: Converts refresh-token columns from the reader's current row into a domain object through the controlled <see cref="RefreshToken.Restore"/> factory.
    /// </summary>
    /// <param name="reader">TR: Refresh-token kolonlarını içeren açık SqlDataReader. EN: Open SqlDataReader containing refresh-token columns.</param>
    /// <param name="prefix">TR: Join sorgularında refresh-token kolon alias prefix'i. EN: Refresh-token column-alias prefix in join queries.</param>
    /// <returns>TR: Kalıcı state'ten yeniden oluşturulan refresh token nesnesini döndürür. EN: Returns the refresh-token object rehydrated from persisted state.</returns>
    public static RefreshToken ReadRefreshToken(SqlDataReader reader, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(reader);
        var consumedAtOrdinal = reader.GetOrdinal($"{prefix}ConsumedAt");
        var revokedAtOrdinal = reader.GetOrdinal($"{prefix}RevokedAt");
        var replacedByOrdinal = reader.GetOrdinal($"{prefix}ReplacedByTokenId");

        return RefreshToken.Restore(
            reader.GetGuid(reader.GetOrdinal($"{prefix}Id")),
            reader.GetGuid(reader.GetOrdinal($"{prefix}SessionId")),
            reader.GetString(reader.GetOrdinal($"{prefix}TokenHash")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}CreatedAt")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal($"{prefix}ExpiresAt")),
            reader.IsDBNull(consumedAtOrdinal)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(consumedAtOrdinal),
            reader.IsDBNull(revokedAtOrdinal)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(revokedAtOrdinal),
            reader.IsDBNull(replacedByOrdinal)
                ? null
                : reader.GetGuid(replacedByOrdinal));
    }
}

using System;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NpgsqlTypes;
using NUnit.Framework;

namespace Npgsql.Tests.Types;

public class NumericTests : MultiplexingTestBase
{
    static readonly object[] ReadWriteCases = new[]
    {
        new object[] { "0.0000000000000000000000000001::numeric", 0.0000000000000000000000000001M },
        new object[] { "0.000000000000000000000001::numeric", 0.000000000000000000000001M },
        new object[] { "0.00000000000000000001::numeric", 0.00000000000000000001M },
        new object[] { "0.0000000000000001::numeric", 0.0000000000000001M },
        new object[] { "0.000000000001::numeric", 0.000000000001M },
        new object[] { "0.00000001::numeric", 0.00000001M },
        new object[] { "0.0001::numeric", 0.0001M },
        new object[] { "1::numeric", 1M },
        new object[] { "10000::numeric", 10000M },
        new object[] { "100000000::numeric", 100000000M },
        new object[] { "1000000000000::numeric", 1000000000000M },
        new object[] { "10000000000000000::numeric", 10000000000000000M },
        new object[] { "100000000000000000000::numeric", 100000000000000000000M },
        new object[] { "1000000000000000000000000::numeric", 1000000000000000000000000M },
        new object[] { "10000000000000000000000000000::numeric", 10000000000000000000000000000M },

        new object[] { "1E-28::numeric", 0.0000000000000000000000000001M },
        new object[] { "1E-24::numeric", 0.000000000000000000000001M },
        new object[] { "1E-20::numeric", 0.00000000000000000001M },
        new object[] { "1E-16::numeric", 0.0000000000000001M },
        new object[] { "1E-12::numeric", 0.000000000001M },
        new object[] { "1E-8::numeric", 0.00000001M },
        new object[] { "1E-4::numeric", 0.0001M },
        new object[] { "1E+0::numeric", 1M },
        new object[] { "1E+4::numeric", 10000M },
        new object[] { "1E+8::numeric", 100000000M },
        new object[] { "1E+12::numeric", 1000000000000M },
        new object[] { "1E+16::numeric", 10000000000000000M },
        new object[] { "1E+20::numeric", 100000000000000000000M },
        new object[] { "1E+24::numeric", 1000000000000000000000000M },
        new object[] { "1E+28::numeric", 10000000000000000000000000000M },

        new object[] { "11.222233334444555566667777888::numeric", 11.222233334444555566667777888M },
        new object[] { "111.22223333444455556666777788::numeric", 111.22223333444455556666777788M },
        new object[] { "1111.2222333344445555666677778::numeric", 1111.2222333344445555666677778M },

        new object[] { "+79228162514264337593543950335::numeric", +79228162514264337593543950335M },
        new object[] { "-79228162514264337593543950335::numeric", -79228162514264337593543950335M },

        // It is important to test rounding on both even and odd
        // numbers to make sure midpoint rounding is away from zero.
        new object[] { "1::numeric(10,2)", 1.00M },
        new object[] { "2::numeric(10,2)", 2.00M },

        new object[] { "1.2::numeric(10,1)", 1.2M },
        new object[] { "1.2::numeric(10,2)", 1.20M },
        new object[] { "1.2::numeric(10,3)", 1.200M },
        new object[] { "1.2::numeric(10,4)", 1.2000M },
        new object[] { "1.2::numeric(10,5)", 1.20000M },

        new object[] { "1.4::numeric(10,0)", 1M },
        new object[] { "1.5::numeric(10,0)", 2M },
        new object[] { "2.4::numeric(10,0)", 2M },
        new object[] { "2.5::numeric(10,0)", 3M },

        new object[] { "-1.4::numeric(10,0)", -1M },
        new object[] { "-1.5::numeric(10,0)", -2M },
        new object[] { "-2.4::numeric(10,0)", -2M },
        new object[] { "-2.5::numeric(10,0)", -3M },

        // Bug 2033
        new object[] { "0.0036882500000000000000000000", 0.0036882500000000000000000000M },

        new object[] { "936490726837837729197", 936490726837837729197M },
        new object[] { "9364907268378377291970000", 9364907268378377291970000M },
        new object[] { "3649072683783772919700000000", 3649072683783772919700000000M },
        new object[] { "1234567844445555.000000000", 1234567844445555.000000000M },
        new object[] { "11112222000000000000", 11112222000000000000M },
        new object[] { "0::numeric", 0M },

        // The following exercise NumericHandler.ReadRounded's fraction normalization: a wide-enough
        // numeric routes through the rounding read path, which must reproduce the exact dscale Postgres
        // reports rather than whatever scale the wire's base-10000 digit groups happen to render.

        // Fraction shorter than dscale: no fractional groups are sent for a round-number cast to a
        // wide numeric(_,3), so the read must pad with zeros to reach the requested scale.
        new object[] { "12345678901234567890123456::numeric(30,3)", 12345678901234567890123456.000M },

        // Fraction exactly matches dscale: no padding or trimming needed.
        new object[] { "12345678901234567890123456.78::numeric", 12345678901234567890123456.78M },

        // Fraction longer than dscale: Postgres always transmits whole 4-digit digit groups, so a
        // dscale of 1 still renders a full trailing group (e.g. "5000"); the excess zeros within that
        // group must be trimmed to match the real dscale exactly.
        new object[] { "12345678901234567890123456.5::numeric(30,1)", 12345678901234567890123456.5M },

        // dscale wider than System.Decimal can hold (> 28) is capped: the fraction is padded out to
        // exactly 28 digits rather than the requested 30.
        new object[] { "1::numeric(31,30)", 1.0000000000000000000000000000M },

        new object[] { "-12345678901234567890123456.5::numeric(30,1)", -12345678901234567890123456.5M },
        new object[] { "-12345678901234567890123456::numeric(30,3)", -12345678901234567890123456.000M },
        new object[] { "12345678901234567890123456::numeric(30,0)", 12345678901234567890123456M },
    };

    [Test]
    [TestCaseSource(nameof(ReadWriteCases))]
    public async Task Read(string query, decimal expected)
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT " + query, conn);
        Assert.That(
            decimal.GetBits((decimal)(await cmd.ExecuteScalarAsync())!),
            Is.EqualTo(decimal.GetBits(expected)));
    }

    [Test]
    [TestCaseSource(nameof(ReadWriteCases))]
    public async Task Write(string query, decimal expected)
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT @p, @p = " + query, conn);
        cmd.Parameters.AddWithValue("p", expected);
        using var rdr = await cmd.ExecuteReaderAsync();
        rdr.Read();
        Assert.That(decimal.GetBits(rdr.GetFieldValue<decimal>(0)), Is.EqualTo(decimal.GetBits(expected)));
        Assert.That(rdr.GetFieldValue<bool>(1));
    }


    [Test]
    public async Task Numeric()
    {
        await AssertType(5.5m, "5.5", "numeric", NpgsqlDbType.Numeric, DbType.Decimal);
        await AssertTypeWrite(5.5m, "5.5", "numeric", NpgsqlDbType.Numeric, DbType.VarNumeric, inferredDbType: DbType.Decimal);

        await AssertType((short)8, "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
        await AssertType(8,        "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
        await AssertType((byte)8,  "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
        await AssertType(8F,       "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
        await AssertType(8D,       "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
        await AssertType(8M,       "8", "numeric", NpgsqlDbType.Numeric, DbType.Decimal, isDefault: false);
    }

    [Test, Description("Tests that a numeric wider than a System.Decimal is rounded to the nearest representable value, the value is read wholly, and it is safe to continue reading")]
    public async Task Read_overflow_is_safe()
    {
        using var conn = await OpenConnectionAsync();
        // This 29-digit number used to cause an OverflowException; it now rounds to 28 digits
        // with decimal.Parse semantics, exactly as the v2.x text protocol did (the exact
        // midpoint at digit 29 resolves to the truncated value under Parse's rounding).
        // It is important to have an unread column after the wide one to prove the read consumed
        // the whole value and the reader stays usable in ReaderState.InResult.
        using var cmd = new NpgsqlCommand(@"SELECT (0.20285714285714285714285714285)::numeric, generate_series FROM generate_series(1, 2)", conn);
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        var i = 1;

        while (reader.Read())
        {
            Assert.That(reader.GetDecimal(0), Is.EqualTo(0.2028571428571428571428571428m));
            var intValue = reader.GetInt32(1);

            Assert.That(intValue, Is.EqualTo(i++));
            Assert.That(conn.FullState, Is.EqualTo(ConnectionState.Open | ConnectionState.Fetching));
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Open));
            Assert.That(reader.State, Is.EqualTo(ReaderState.InResult));
        }
    }

    [Test, Description("High-dscale values whose trailing zero digit groups were stripped from the wire are read exactly, not corrupted")]
    public async Task Read_high_dscale_value_with_stripped_trailing_zero_groups()
    {
        // Postgres strips trailing zero base-10000 digit groups before sending, so a value's
        // stored groups can end well before its display scale. 1::numeric(38,33) is transmitted
        // as a single group [1] with dscale 33. A product of two numeric(28,20) columns has
        // dscale 40, so every round-number rate or multiplier product takes this shape.
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT
            1::numeric(38,33),
            -1::numeric(38,33),
            1::numeric(28,20) * 1::numeric(28,20) * 1::numeric(28,20),
            0.5::numeric(28,20) * 1::numeric(28,20),
            1.0000000001::numeric(28,20) * 1.0000000001::numeric(28,20)", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.That(reader.GetDecimal(0), Is.EqualTo(1m));
        Assert.That(reader.GetDecimal(1), Is.EqualTo(-1m));
        Assert.That(reader.GetDecimal(2), Is.EqualTo(1m));
        Assert.That(reader.GetDecimal(3), Is.EqualTo(0.5m));
        // 21 significant digits: fits in a decimal untouched, so no digits may be lost.
        Assert.That(reader.GetDecimal(4), Is.EqualTo(1.00000000020000000001m));
    }

    [Test, Description("Overflow from total significant digits (rather than scale) rounds to the nearest representable decimal")]
    public async Task Read_rounds_width_overflow_to_nearest_representable()
    {
        // 46320.903225806451612903225806448 is scale 27 but 32 significant digits: the overflow
        // comes from total width, not dscale. It rounds to 29 digits, which fit.
        // The 29 nines exercise carry propagation: rounding up adds a digit and the fit must be re-checked.
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT
            46320.903225806451612903225806448::numeric,
            -46320.903225806451612903225806448::numeric,
            0.99999999999999999999999999999::numeric", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.That(reader.GetDecimal(0), Is.EqualTo(46320.903225806451612903225806m));
        Assert.That(reader.GetDecimal(1), Is.EqualTo(-46320.903225806451612903225806m));
        Assert.That(reader.GetDecimal(2), Is.EqualTo(1m));
    }

    [Test, Description("A value whose integer part cannot fit in a decimal at any scale still throws")]
    public async Task Read_integer_part_overflow_still_throws()
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT 123456789012345678901234567890::numeric, 1e30::numeric", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.That(() => reader.GetDecimal(0),
            Throws.Exception
                .With.TypeOf<OverflowException>()
                .With.Message.EqualTo("Numeric value does not fit in a System.Decimal"));
        Assert.That(() => reader.GetDecimal(1),
            Throws.Exception
                .With.TypeOf<OverflowException>()
                .With.Message.EqualTo("Numeric value does not fit in a System.Decimal"));
    }

    [Test]
    [TestCaseSource(nameof(ReadWriteCases))]
    public async Task Read_BigInteger(string query, decimal expected)
    {
        if (decimal.Floor(expected) == expected)
        {
            var bigInt = new BigInteger(expected);
            using var conn = await OpenConnectionAsync();
            using var cmd = new NpgsqlCommand("SELECT " + query, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            await rdr.ReadAsync();
            Assert.That(rdr.GetFieldValue<BigInteger>(0), Is.EqualTo(bigInt));
        }
    }

    [Test]
    [TestCaseSource(nameof(ReadWriteCases))]
    public async Task Write_BigInteger(string query, decimal expected)
    {
        if (decimal.Floor(expected) == expected)
        {
            var bigInt = new BigInteger(expected);
            using var conn = await OpenConnectionAsync();
            using var cmd = new NpgsqlCommand("SELECT @p, @p = " + query, conn);
            cmd.Parameters.AddWithValue("p", bigInt);
            using var rdr = await cmd.ExecuteReaderAsync();
            await rdr.ReadAsync();
            Assert.That(rdr.GetFieldValue<BigInteger>(0), Is.EqualTo(bigInt));
            Assert.That(rdr.GetFieldValue<bool>(1));
        }
    }

    [Test]
    public async Task BigInteger_large()
    {
        var num = BigInteger.Parse(string.Join("", Enumerable.Range(0, 17000).Select(i => ((i + 1) % 10).ToString())));
        using var conn = await OpenConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT '0.1'::numeric, @p", conn);
        cmd.Parameters.AddWithValue("p", num);
        using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        await rdr.ReadAsync();
        Assert.Throws<InvalidCastException>(() => rdr.GetFieldValue<BigInteger>(0));
        Assert.That(rdr.GetFieldValue<BigInteger>(1), Is.EqualTo(num));
    }

    public NumericTests(MultiplexingMode multiplexingMode) : base(multiplexingMode) {}
}

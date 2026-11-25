using System.Diagnostics;
using System.Globalization;
using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

public sealed class DimFechaLoader : IDimensionLoader
{
    private static readonly string[] NombresDias =
    [
        "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"
    ];

    private static readonly string[] NombresMeses =
    [
        "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private readonly string _sourceConn;
    private readonly string _dwConn;

    public string DimensionName => "DimFecha";

    public DimFechaLoader(IConfiguration configuration)
    {
        _sourceConn = SqlHelper.GetRequiredConnection(configuration, "SourceDb");
        _dwConn = SqlHelper.GetRequiredConnection(configuration, "DataWarehouseDb");
    }

    public async Task<DimensionLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int extracted = 0, inserted = 0, updated = 0, skipped = 0;

        try
        {
            var dates = new SortedSet<DateTime>();
            await using (var src = new SqlConnection(_sourceConn))
            {
                await src.OpenAsync(cancellationToken);
                const string sql = @"
                    SELECT DISTINCT CAST(FechaVenta AS DATE) AS Fecha FROM dbo.Venta
                    UNION
                    SELECT DISTINCT FechaRegistro FROM dbo.Cliente
                    UNION
                    SELECT DISTINCT FechaIngreso FROM dbo.Empleado
                    UNION
                    SELECT DISTINCT FechaApertura FROM dbo.Sucursal";
                await using var cmd = new SqlCommand(sql, src);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        dates.Add(reader.GetDateTime(0).Date);
                    }
                }
            }

            extracted = dates.Count;

            await using (var dw = new SqlConnection(_dwConn))
            {
                await dw.OpenAsync(cancellationToken);
                foreach (var d in dates)
                {
                    int fechaKey = d.Year * 10000 + d.Month * 100 + d.Day;

                    const string checkSql = "SELECT COUNT(1) FROM dbo.DimFecha WHERE FechaKey = @Key";
                    await using (var checkCmd = new SqlCommand(checkSql, dw))
                    {
                        checkCmd.Parameters.AddWithValue("@Key", fechaKey);
                        var exists = (int)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
                        if (exists)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    byte dia = (byte)d.Day;
                    string nombreDia = NombresDias[(int)d.DayOfWeek];
                    byte semana = (byte)ISOWeek.GetWeekOfYear(d);
                    byte mes = (byte)d.Month;
                    string nombreMes = NombresMeses[d.Month];
                    byte trimestre = (byte)((d.Month - 1) / 3 + 1);
                    short anio = (short)d.Year;
                    bool esFinDeSemana = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    const string insSql = @"
                        INSERT INTO dbo.DimFecha
                            (FechaKey, Fecha, Dia, NombreDia, Semana, Mes, NombreMes, Trimestre, Anio, EsFinDeSemana)
                        VALUES
                            (@FechaKey, @Fecha, @Dia, @NombreDia, @Semana, @Mes, @NombreMes, @Trimestre, @Anio, @EsFinDeSemana)";
                    await using var ins = new SqlCommand(insSql, dw);
                    ins.Parameters.AddWithValue("@FechaKey", fechaKey);
                    ins.Parameters.AddWithValue("@Fecha", d);
                    ins.Parameters.AddWithValue("@Dia", dia);
                    ins.Parameters.AddWithValue("@NombreDia", nombreDia);
                    ins.Parameters.AddWithValue("@Semana", semana);
                    ins.Parameters.AddWithValue("@Mes", mes);
                    ins.Parameters.AddWithValue("@NombreMes", nombreMes);
                    ins.Parameters.AddWithValue("@Trimestre", trimestre);
                    ins.Parameters.AddWithValue("@Anio", anio);
                    ins.Parameters.AddWithValue("@EsFinDeSemana", esFinDeSemana);
                    await ins.ExecuteNonQueryAsync(cancellationToken);
                    inserted++;
                }
            }

            sw.Stop();
            return new DimensionLoadResult(DimensionName, extracted, inserted, updated, skipped, sw.ElapsedMilliseconds, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DimensionLoadResult(DimensionName, extracted, inserted, updated, skipped, sw.ElapsedMilliseconds, false, ex.Message);
        }
    }
}

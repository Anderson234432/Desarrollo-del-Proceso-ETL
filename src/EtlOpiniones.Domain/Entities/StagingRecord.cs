namespace EtlOpiniones.Domain.Entities;

public sealed class EncuestaInterna
{
    public string IdEncuesta { get; set; } = string.Empty;
    public string IdCliente { get; set; } = string.Empty;
    public string IdProducto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int PuntajeSatisfaccion { get; set; }
    public string Comentario { get; set; } = string.Empty;
}

public sealed class ResenaDb
{
    public int IdResena { get; set; }
    public int IdCliente { get; set; }
    public int IdProducto { get; set; }
    public DateTime Fecha { get; set; }
    public int Calificacion { get; set; }
    public string TextoResena { get; set; } = string.Empty;
}

public sealed class ComentarioApi
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
}

public sealed class StagingRecord
{
    public string SourceSystem { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string CustomerRef { get; init; } = string.Empty;
    public string ProductRef { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public int? Rating { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime ExtractedAtUtc { get; init; } = DateTime.UtcNow;
}

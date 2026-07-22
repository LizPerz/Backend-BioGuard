using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class SensorService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<SensorService> _logger;

    public SensorService(IMongoDbContext db, ILogger<SensorService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LecturaSensor> InsertarLecturaAsync(string pacienteId, string dispositivoMac,
        int pulsoBpm, double temperaturaC, double sudoracionGsr, double probabilidadPico, int diasHistorial = 30)
    {
        var now = DateTime.UtcNow;
        var lectura = new LecturaSensor
        {
            Meta = new MetaData
            {
                PacienteId = pacienteId,
                DispositivoMac = dispositivoMac
            },
            Timestamp = now,
            PulsoBpm = pulsoBpm,
            TemperaturaC = temperaturaC,
            SudoracionGsr = sudoracionGsr,
            ProbabilidadPico = probabilidadPico,
            ExpireAt = now.AddDays(diasHistorial)
        };

        await _db.LecturasSensores.InsertOneAsync(lectura);
        _logger.LogInformation("Sensor reading inserted for patient: {PacienteId}", pacienteId);
        return lectura;
    }

    public async Task<List<LecturaSensor>> ObtenerLecturasAsync(string pacienteId, int limite = 100)
    {
        var filter = Builders<LecturaSensor>.Filter.Eq(l => l.Meta.PacienteId, pacienteId);
        var sort = Builders<LecturaSensor>.Sort.Descending(l => l.Timestamp);
        return await _db.FindToListAsync(_db.LecturasSensores, filter, sort, limite);
    }

    public async Task<List<LecturaSensor>> ObtenerLecturasRangoAsync(
        string pacienteId, DateTime desde, DateTime hasta)
    {
        var filter = Builders<LecturaSensor>.Filter.And(
            Builders<LecturaSensor>.Filter.Eq(l => l.Meta.PacienteId, pacienteId),
            Builders<LecturaSensor>.Filter.Gte(l => l.Timestamp, desde),
            Builders<LecturaSensor>.Filter.Lte(l => l.Timestamp, hasta)
        );
        var sort = Builders<LecturaSensor>.Sort.Descending(l => l.Timestamp);
        return await _db.FindToListAsync(_db.LecturasSensores, filter, sort);
    }

    public async Task<EventoMetabolico> CrearEventoAsync(string pacienteId, double probabilidad,
        string nivelRiesgo, string descripcion, double? longitud = null, double? latitud = null)
    {
        var evento = new EventoMetabolico
        {
            PacienteId = pacienteId,
            ProbabilidadMl = probabilidad,
            NivelRiesgo = nivelRiesgo,
            Descripcion = descripcion,
            FechaEvento = DateTime.UtcNow,
            UbicacionGps = longitud.HasValue && latitud.HasValue
                ? new UbicacionGps { Coordinates = new[] { longitud.Value, latitud.Value } }
                : null,
            Atendida = false
        };

        await _db.EventosMetabolicos.InsertOneAsync(evento);
        _logger.LogInformation("Metabolic event created for patient: {PacienteId}", pacienteId);
        return evento;
    }

    public async Task<List<EventoMetabolico>> ObtenerEventosAsync(string pacienteId, int limite = 50)
    {
        var filter = Builders<EventoMetabolico>.Filter.Eq(e => e.PacienteId, pacienteId);
        var sort = Builders<EventoMetabolico>.Sort.Descending(e => e.FechaEvento);
        return await _db.FindToListAsync(_db.EventosMetabolicos, filter, sort, limite);
    }

    public async Task<bool> AtenderEventoAsync(string eventoId, string cuidadorId)
    {
        var update = Builders<EventoMetabolico>.Update
            .Set(e => e.Atendida, true)
            .Set(e => e.AtendidoPorId, cuidadorId)
            .Set(e => e.FechaAtencion, DateTime.UtcNow);

        var result = await _db.EventosMetabolicos.UpdateOneAsync(e => e.Id == eventoId, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogWarning("Event not found or already attended: {EventoId}", eventoId);
        }
        else
        {
            _logger.LogInformation("Event attended: {EventoId} by caregiver: {CuidadorId}", eventoId, cuidadorId);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AgregarAccionAsync(string eventoId, string accion)
    {
        var evento = await _db.FindFirstOrDefaultAsync(_db.EventosMetabolicos, e => e.Id == eventoId);
        if (evento == null)
        {
            _logger.LogWarning("Add action to non-existent event: {EventoId}", eventoId);
            return false;
        }

        var nuevasAcciones = string.IsNullOrEmpty(evento.AccionesTomadas)
            ? accion
            : $"{evento.AccionesTomadas}; {accion}";

        var update = Builders<EventoMetabolico>.Update.Set(e => e.AccionesTomadas, nuevasAcciones);
        var result = await _db.EventosMetabolicos.UpdateOneAsync(e => e.Id == eventoId, update);
        return result.ModifiedCount > 0;
    }

    public async Task InsertarTrackingAsync(string pacienteId, string mac,
        double longitud, double latitud, bool esEmergencia)
    {
        var tracking = new TrackingGps
        {
            Meta = new MetaData { PacienteId = pacienteId, DispositivoMac = mac },
            Timestamp = DateTime.UtcNow,
            Ubicacion = new UbicacionGps { Coordinates = new[] { longitud, latitud } },
            EsEmergencia = esEmergencia
        };

        await _db.TrackingGps.InsertOneAsync(tracking);
        _logger.LogInformation("GPS tracking inserted for patient: {PacienteId}, emergency: {EsEmergencia}", pacienteId, esEmergencia);
    }

    public async Task<List<TrackingGps>> ObtenerTrackingAsync(string pacienteId, int limite = 100)
    {
        var filter = Builders<TrackingGps>.Filter.Eq(t => t.Meta.PacienteId == pacienteId, true);
        var sort = Builders<TrackingGps>.Sort.Descending(t => t.Timestamp);
        return await _db.FindToListAsync(_db.TrackingGps, filter, sort, limite);
    }

    public async Task<List<TrackingGps>> ObtenerTrackingRangoAsync(
        string pacienteId, DateTime desde, DateTime hasta)
    {
        var filter = Builders<TrackingGps>.Filter.And(
            Builders<TrackingGps>.Filter.Eq(t => t.Meta.PacienteId, pacienteId),
            Builders<TrackingGps>.Filter.Gte(t => t.Timestamp, desde),
            Builders<TrackingGps>.Filter.Lte(t => t.Timestamp, hasta)
        );
        var sort = Builders<TrackingGps>.Sort.Descending(t => t.Timestamp);
        return await _db.FindToListAsync(_db.TrackingGps, filter, sort);
    }

    public async Task<TrackingGps?> ObtenerUltimaUbicacionAsync(string pacienteId)
    {
        var filter = Builders<TrackingGps>.Filter.Eq(t => t.Meta.PacienteId, pacienteId);
        var sort = Builders<TrackingGps>.Sort.Descending(t => t.Timestamp);
        return await _db.FindFirstOrDefaultAsync(_db.TrackingGps, filter, sort);
    }
}

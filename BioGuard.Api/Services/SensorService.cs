using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class SensorService
{
    private readonly IMongoDbContext _db;

    public SensorService(IMongoDbContext db) => _db = db;

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
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AgregarAccionAsync(string eventoId, string accion)
    {
        var evento = await _db.FindFirstOrDefaultAsync(_db.EventosMetabolicos, e => e.Id == eventoId);
        if (evento == null) return false;

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

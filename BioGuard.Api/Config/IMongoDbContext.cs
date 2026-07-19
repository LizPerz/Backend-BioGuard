using System.Linq.Expressions;
using MongoDB.Driver;
using BioGuard.Api.Models;

namespace BioGuard.Api.Config;

public interface IMongoDbContext
{
    IMongoCollection<Plan> Planes { get; }
    IMongoCollection<UsuarioWeb> UsuariosWeb { get; }
    IMongoCollection<Paciente> Pacientes { get; }
    IMongoCollection<Cuidador> Cuidadores { get; }
    IMongoCollection<Dispositivo> Dispositivos { get; }
    IMongoCollection<LecturaSensor> LecturasSensores { get; }
    IMongoCollection<EventoMetabolico> EventosMetabolicos { get; }
    IMongoCollection<TrackingGps> TrackingGps { get; }
    IMongoCollection<Notificacion> Notificaciones { get; }
    IMongoCollection<Auditoria> Auditoria { get; }
    IMongoCollection<Pago> Pagos { get; }
    IMongoCollection<PrediccionMl> PrediccionesMl { get; }
    IMongoCollection<ModeloMl> ModelosMl { get; }
    IMongoCollection<FcmToken> FcmTokens { get; }
    IMongoCollection<RefreshToken> RefreshTokens { get; }
    IMongoCollection<Medicamento> Medicamentos { get; }
    IMongoCollection<Alerta> Alertas { get; }

    Task<T?> FindFirstOrDefaultAsync<T>(IMongoCollection<T> collection, Expression<Func<T, bool>> filter);
    Task<List<T>> FindToListAsync<T>(IMongoCollection<T> collection, Expression<Func<T, bool>> filter);
    Task<List<T>> FindToListAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T>? sort = null, int? limit = null, int? skip = null);
    Task<T?> FindFirstOrDefaultAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T>? sort = null);
    Task<DeleteResult> DeleteManyAsync<T>(IMongoCollection<T> collection, Expression<Func<T, bool>> filter);
    Task<UpdateResult> UpdateOneAsync<T>(IMongoCollection<T> collection, Expression<Func<T, bool>> filter, UpdateDefinition<T> update);
    Task<long> CountDocumentsAsync<T>(IMongoCollection<T> collection, Expression<Func<T, bool>> filter);
}

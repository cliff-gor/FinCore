using System.Linq.Expressions;
using FinCore.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinCore.BuildingBlocks.Infrastructure.Persistence.Converters;

/// <summary>Stores <c>readonly record struct XxxId(Guid Value)</c> as a plain <c>uuid</c>.</summary>
public sealed class StronglyTypedIdConverter<TId>(Expression<Func<Guid, TId>> fromGuid)
    : ValueConverter<TId, Guid>(id => id.Value, fromGuid)
    where TId : struct, IStronglyTypedId;

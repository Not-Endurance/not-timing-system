using System.Reflection;
using System.Text.Json;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// What making or changing a row of a collection of documents is, for the families of reference data and for everything else
/// that is kept as a document and written member by member (the Setup of an Event, what an Event keeps): the members a
/// document names are put into the row, the row has to be one the domain accepts, and then the members that were named, and no
/// others, are written. It is no endpoint, so that the endpoints of other features do not call the endpoints of this one.
/// </summary>
internal static class ReferenceRows
{
    /// <summary>
    /// The refusal of a row the domain does not accept: it is made into the entity it stands for, which is where its rules
    /// are, and the first one it breaks is 422 with the member it is about.
    /// </summary>
    public static IResult? Invalid<TModel, TEntity>(TModel row)
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        try
        {
            row.MapToEntity();
            return null;
        }
        catch (ValidationException ex)
        {
            return ReferenceFailures.InvalidAttribute.With(ex.Message).AsResult();
        }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException or InvalidOperationException)
        {
            return ReferenceFailures.MissingMember.AsResult();
        }
    }

    /// <summary>
    /// What a change of a row is, once the caller may make it: the members the document names are put into the row, the row
    /// has to be one the domain accepts, and then the members that were named, and no others, are written. Null when the row
    /// was changed, and the answer that refuses it when not.
    /// </summary>
    public static async Task<IResult?> ChangeAsync<TModel, TEntity>(
        ReferenceMembers<TModel> members,
        IReferenceCollection<TModel> collection,
        TModel row,
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var named = new List<PropertyInfo>();
        if (members.Apply(row, attributes, named) is { } malformed)
        {
            return malformed;
        }

        if (Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var update = members.UpdateOf(row, named);
        return update.ElementCount > 0 && !await collection.UpdateAsync(row.Id, update, cancellationToken)
            ? JsonApiResults.NotFound()
            : null;
    }

    /// <summary>Gives a row the id its client made for it, which a document names as the id of the resource and never as an attribute.</summary>
    public static void SetId<TModel>(TModel row, Guid id)
        where TModel : class
    {
        typeof(TModel).GetProperty("Id")!.SetValue(row, id);
    }
}

using System.Reflection;
using Not.Application.Authentication.User;
using Not.Domain;
using Not.Krud.Abstractions;
using Not.Storage;
using Not.Structures;
using NoTiming.Api.Features.Live;
using NoTiming.Ui;
using NTS.Contracts.Live;
using NTS.Domain.Aggregates;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009: every Id is a Guid, in the domain, in storage and on the wire. A new int id anywhere in our code, in a
/// model, a hub payload or a route, fails here. Start numbers, distances and counts stay ints; they are not ids.
/// </summary>
public sealed class IdentifierShapeTests
{
    const BindingFlags EVERYTHING =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;
    static readonly Dictionary<string, string> NOT_YET_GUIDS = new()
    {
        // Identifiers that are not Guids yet, and why. Naming them here means a new one cannot hide.
        ["TenantId"] = "the constant \"nts\" until ADR-0012 puts a real tenant id on every document (#643)",
        ["AccountId"] = "SettingModel keeps the text of a Guid; the Setting aggregate is deleted by ADR-0012",
        ["MongoId"] = "the ObjectId _id of a session or pending-snapshots document, a storage key next to its Guid Id",
    };
    static readonly Assembly[] OUR_ASSEMBLIES =
    [
        typeof(IIdentifiable).Assembly, // Not
        typeof(Entity).Assembly, // Not.Domain
        typeof(NUserModel).Assembly, // Not.Application
        typeof(IKrudFormModel).Assembly, // Not.Krud
        typeof(NStorageBuilder).Assembly, // Not.Storage
        typeof(Country).Assembly, // NTS.Domain
        typeof(NTS.Domain.Setup.Aggregates.Athlete).Assembly, // NTS.Domain.Setup
        typeof(NTS.Domain.Core.Aggregates.Participation).Assembly, // NTS.Domain.Core
        typeof(ILiveClientProcedures).Assembly, // NTS.Contracts
        typeof(NTS.Application.Factories.ParticipationAndRankingFactory).Assembly, // NTS.Application
        typeof(NoTimingUiServices).Assembly, // NoTiming.Ui
        typeof(LiveHub).Assembly, // NoTiming.Api
        typeof(MongoConstants).Assembly, // NTS.Nexus.HTTP
    ];

    [Fact]
    public void No_integer_identifier_remains_in_our_code()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in OUR_ASSEMBLIES.SelectMany(TypesOf))
        {
            foreach (var property in type.GetProperties(EVERYTHING).Where(x => IsIdentifierName(x.Name)))
            {
                Check(offenders, property.PropertyType, $"{type.FullName}.{property.Name} (property)");
            }

            foreach (var field in type.GetFields(EVERYTHING).Where(x => IsIdentifierName(x.Name)))
            {
                Check(offenders, field.FieldType, $"{type.FullName}.{field.Name} (field)");
            }

            foreach (
                var method in type.GetMethods(EVERYTHING).Cast<MethodBase>().Concat(type.GetConstructors(EVERYTHING))
            )
            {
                if (method is MethodInfo { Name: var name } info && IsIdentifierName(name))
                {
                    Check(offenders, info.ReturnType, $"{type.FullName}.{name} (return type)");
                }

                foreach (var parameter in method.GetParameters().Where(x => IsIdentifierName(x.Name)))
                {
                    Check(offenders, parameter.ParameterType, $"{type.FullName}.{method.Name}({parameter.Name})");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Integer identifiers remain:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Every_Id_and_foreign_key_of_an_entity_a_document_or_a_model_is_a_Guid()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var models = OUR_ASSEMBLIES
            .SelectMany(TypesOf)
            .Where(x =>
                x.IsClass
                && (typeof(IIdentifiable).IsAssignableFrom(x) || x.Name.EndsWith("Model", StringComparison.Ordinal))
            );

        foreach (var type in models)
        {
            foreach (
                var property in type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            {
                if (
                    !IsIdentifierName(property.Name)
                    || IsExternalIdentifier(property.Name)
                    || NOT_YET_GUIDS.ContainsKey(property.Name)
                )
                {
                    continue;
                }

                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (propertyType != typeof(Guid))
                {
                    offenders.Add($"{type.FullName}.{property.Name} is {property.PropertyType.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Identifiers that are not Guids:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Routes_take_Guid_ids()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in OUR_ASSEMBLIES.SelectMany(TypesOf))
        {
            foreach (var template in RouteTemplatesOf(type))
            {
                if (template.Contains(":int", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{type.FullName}: {template}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Routes that take an int:\n" + string.Join("\n", offenders));
    }

    static void Check(ISet<string> offenders, Type type, string description)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (
            underlying == typeof(int)
            || underlying == typeof(long)
            || underlying == typeof(short)
            || underlying == typeof(uint)
            || underlying == typeof(ulong)
        )
        {
            offenders.Add($"{description} is {type.Name}");
        }
    }

    static bool IsIdentifierName(string? name)
    {
        return name != null
            && !name.StartsWith('<')
            && (name is "Id" or "id" or "_id" || name.EndsWith("Id", StringComparison.Ordinal));
    }

    /// <summary>The federation's identifiers (a FEI id, a FEI event id) are not ours and are not Guids.</summary>
    static bool IsExternalIdentifier(string name)
    {
        return name.StartsWith("Fei", StringComparison.Ordinal);
    }

    static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x => x != null)!;
        }
    }

    static IEnumerable<string> RouteTemplatesOf(Type type)
    {
        var attributes = new List<CustomAttributeData>(type.GetCustomAttributesData());
        foreach (var member in type.GetMembers(EVERYTHING))
        {
            attributes.AddRange(member.GetCustomAttributesData());
            if (member is MethodBase method)
            {
                foreach (var parameter in method.GetParameters())
                {
                    attributes.AddRange(parameter.GetCustomAttributesData());
                }
            }
        }

        foreach (var data in attributes)
        {
            if (data.AttributeType.Name is not ("HttpTriggerAttribute" or "RouteAttribute"))
            {
                continue;
            }

            var arguments = data
                .ConstructorArguments.Select(x => x.Value)
                .Concat(data.NamedArguments.Select(x => x.TypedValue.Value));
            foreach (var argument in arguments)
            {
                if (argument is string text && text.Contains('{'))
                {
                    yield return text;
                }
            }
        }
    }
}

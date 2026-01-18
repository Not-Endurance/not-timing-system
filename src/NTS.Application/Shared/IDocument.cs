using Not.Application.Tenants;
using Not.Domain.Aggregates;
using Not.Structures;

namespace NTS.Application.Shared;

// TODO: remove IAggregateRoot once only models are used in repositories and use IDocument in IRepository instead
public interface    IDocument : IIdentifiable, ITenantAware, IAggregate { }

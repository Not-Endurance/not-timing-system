using NC.Application.Tenants;
using NC.Domain.Abstractions;
using NC.Structures;

namespace NoTiming.Application.Contracts.Shared;

// TODO: remove IAggregateRoot once only models are used in repositories and use IDocument in IRepository instead
public interface IDocument : IIdentifiable, ITenantAware, IAggregate { }

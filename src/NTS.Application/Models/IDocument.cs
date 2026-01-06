using Not.Application.Tenants;
using Not.Domain.Aggregates;
using Not.Structures;

namespace NTS.Application.Models;

// TODO: remove IAggregateRoot once only models are used in repositories and use IDocument in IRepository instead
public interface IDocument : IIdentifiable, ITenantAware, IAggregateRoot { }

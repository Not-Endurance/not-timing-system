using System.Linq.Expressions;
using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class EnduranceEventRepository : EventScopedApiRepository<EnduranceEvent, EnduranceEventModel>, ITransient
{
    const string ROOT_REPOSITORY_EXCEPTION =
        "Only Create, Read and Update operations are implemented for Root entities.";

    public EnduranceEventRepository(NHttpClient client, IServiceProvider serviceProvider)
        : base("endurance-event", client, serviceProvider) { }

    public override Task<EnduranceEvent?> Read(Expression<Func<EnduranceEvent, bool>> _)
    {
        return Read(0);
    }

    public override Task<IEnumerable<EnduranceEvent>> ReadMany()
    {
        throw new ApplicationException(ROOT_REPOSITORY_EXCEPTION);
    }

    public override Task<IEnumerable<EnduranceEvent>> ReadMany(Expression<Func<EnduranceEvent, bool>> filter)
    {
        throw new ApplicationException(ROOT_REPOSITORY_EXCEPTION);
    }
}

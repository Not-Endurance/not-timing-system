namespace Not.Application.Services;

public interface IListBehind<T> : IDeleteBehind<T>
{
    Task<IEnumerable<T>> ReadMany();
}

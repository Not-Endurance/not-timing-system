namespace Not.Models;

public interface IMapFrom<in T>
{
    void MapFrom(T item);
}

namespace Not.Models;

public interface IMapTo<out T>
{
    T MapTo();
}

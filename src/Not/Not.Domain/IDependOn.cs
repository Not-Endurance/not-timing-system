namespace Not.Domain;

public interface IDependOn<TChild>
{
    void Update(TChild child);
}

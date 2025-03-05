namespace Not.Domain;

public interface ISingleParent<TChild>
{
    void Update(TChild child);
}

public interface ISingleParent { }

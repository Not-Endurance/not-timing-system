using Not.Blazor.CRUD.Ports;

namespace Not.Blazor.CRUD.Forms.Ports;

public interface IFormBehind<T> : ICreateBehind<T>, IUpdateBehind<T> { }

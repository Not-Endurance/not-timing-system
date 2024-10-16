﻿using Not.Injection;
using Not.Domain;

namespace Not.Application.Ports.CRUD;

public interface IUpdate<T> : ITransientService
    where T : DomainEntity
{
    Task<T> Update(T entity);
}

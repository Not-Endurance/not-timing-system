﻿using Common.Conventions;
using Common.Domain;

namespace Common.Application.CRUD;

public interface IUpdate<T> : ITransientService
    where T : DomainEntity
{
    Task<T> Update(T entity); 
}
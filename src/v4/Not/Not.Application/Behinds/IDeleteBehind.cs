﻿using Not.Conventions;
using Not.Domain;

namespace Not.Application.Behinds;

public interface IDeleteBehind<T> : ISingletonService
    where T : DomainEntity
{
    Task<T> Delete(T entity);
}

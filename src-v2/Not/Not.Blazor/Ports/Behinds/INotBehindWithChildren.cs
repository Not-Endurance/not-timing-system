﻿using Not.Domain;

namespace Not.Blazor.Ports.Behinds;

/// <summary>
/// <seealso cref="INotBehindWithChildren{T}"/> represents a <seealso cref="INotBehind"/> that belongs to a tree hierarchy of <seealso cref="DomainEntity"/> objects
/// (see <seealso cref="IParent{T}"/> for more). Details regarding it's parent entity are responsiblity of the implementation
/// </summary>
/// <typeparam name="T">Type of parent entity</typeparam>
public interface INotBehindWithChildren<T>
    where T : DomainEntity, IParent
{
    Task Initialize(int id);
}

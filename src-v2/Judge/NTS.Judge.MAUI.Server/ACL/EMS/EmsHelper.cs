﻿using Not.Domain;

namespace NTS.Judge.MAUI.Server.ACL.EMS;

/// <summary>
/// Shorthand for DomainExceptionBase
/// </summary>
public static class EmsHelper
{
    internal static T Create<T>(string message) where T : EmsDomainExceptionBase, new()
        => EmsDomainExceptionBase.Create<T>(message);

    internal static T Create<T>(string message, params object[] arguments) where T : EmsDomainExceptionBase, new()
        => EmsDomainExceptionBase.Create<T>(message, arguments);

    internal static DomainException Create(string entity, string message)
    {
        var exception = new DomainException(entity, message);
        return exception;
    }
    internal static DomainException Create(string entity, string message, params object[] arguments)
    {
        var exception = new DomainException(entity, message, arguments);
        return exception;
    }
}

namespace Not.Events;

public delegate void EventDelegate();

public delegate void EventDelegate<T>(T payload);

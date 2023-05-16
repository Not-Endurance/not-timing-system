﻿using EMS.Witness.Models;

namespace EMS.Witness.Services;

public class State : IState
{
	public string? ApiHost { get; set; }

	public Dictionary<int, ManualWitnessEvent> WitnessRecords { get; } = new();
}

public interface IState
{
	string? ApiHost { get; }

	Dictionary<int, ManualWitnessEvent> WitnessRecords { get; }
}

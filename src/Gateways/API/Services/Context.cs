﻿using EnduranceJudge.Application;

namespace Endurance.Judge.Gateways.API.Services
{
    public class Context : IContext
    {
        public State State { get; set; }
    }

    public interface IContext
    {
        State State { get; }
    }
}
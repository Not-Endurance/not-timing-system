﻿using System;

namespace Core.Domain.State.Participants;

public class RfidTag : IEquatable<RfidTag>
{
    public const char EMPTY_CHAR = '0';

    public RfidTag() { }
    public RfidTag(string tagData)
    {
        var number = tagData.Substring(0, 3);
        var position = tagData.Substring(3, 6);
        var id = tagData.Substring(9);
        this.Id = id;
        this.Position = position;
        this.ParticipantNumber = int.Parse(number).ToString();
    }

    public string Id { get; set; }
    public string Position { get; set; }
    public string ParticipantNumber { get; set; }

    public bool Equals(RfidTag other)
    {
        return this.Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        if (obj is RfidTag tag)
        {
            return this.Equals(tag);
        }
        return false;
    }

    public override string ToString()
    {
        var number = this.ParticipantNumber.PadLeft(3, EMPTY_CHAR);
        var position = this.Position.PadLeft(6, EMPTY_CHAR);
        return number + position + this.Id;
    }
}   

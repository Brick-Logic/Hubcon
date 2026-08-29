using System;
using System.ComponentModel.DataAnnotations;

namespace HubconTestDomain;

public sealed class NotNullAttribute : ValidationAttribute
{
    public override bool IsDefaultAttribute() => true;
    public override string FormatErrorMessage(string name) => "The value must not be null.";
    public override bool IsValid(object? value) => value != null;
}
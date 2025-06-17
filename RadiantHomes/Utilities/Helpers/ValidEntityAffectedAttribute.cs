namespace RadiantHomes.Utilities.Helpers
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using RadiantHomes.Models;

    public class ValidEntityAffectedAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string stringValue)
            {
                bool isValid = Enum.GetNames(typeof(EntityAffecteds))
                                   .Any(name => name.Equals(stringValue, StringComparison.OrdinalIgnoreCase));
                if (isValid)
                    return ValidationResult.Success;
            }

            return new ValidationResult("DataType must be one of the defined EntityAffecteds enum values.");
        }
    }

}

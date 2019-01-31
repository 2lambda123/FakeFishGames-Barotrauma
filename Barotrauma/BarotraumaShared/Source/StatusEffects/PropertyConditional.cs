﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.Linq;

namespace Barotrauma
{
    // TODO: This class should be refactored: 
    // - Use XElement instead of XAttribute in the constructor
    // - Simplify, remove unnecessary conversions
    // - Improve the flow so that the logic is undestandable.
    // - Maybe ass some test cases for the operators?
    class PropertyConditional
    {
        public enum ConditionType
        {
            PropertyValue,
            Name,
            SpeciesName,
            HasTag,
            HasStatusTag,
            Affliction
        }

        public enum OperatorType
        {
            Equals,
            NotEquals,
            LessThan,
            LessThanEquals,
            GreaterThan,
            GreaterThanEquals
        }

        public readonly ConditionType Type;
        public readonly OperatorType Operator;
        public readonly string AttributeName;
        public readonly string AttributeValue;
        public readonly float? FloatValue;

        public readonly string TargetItemComponentName;

        private readonly string[] afflictionNames = new string[] { "internaldamage", "bleeding", "burn", "oxygenlow", "bloodloss", "pressure", "stun", "husk", "afflictionhusk" };

        private readonly int cancelStatusEffect;

        // TODO: use XElement instead of XAttribute
        public PropertyConditional(XAttribute attribute)
        {
            AttributeName = attribute.Name.ToString().ToLowerInvariant();
            string attributeValueString = attribute.Value.ToString();
            if (string.IsNullOrWhiteSpace(attributeValueString))
            {
                DebugConsole.ThrowError($"Conditional attribute value is empty: {attribute.Parent.ToString()}");
                return;
            }
            string valueString = attributeValueString;
            string[] splitString = valueString.Split(' ');
            if (splitString.Length > 0)
            {
                for (int i = 1; i < splitString.Length; i++)
                {
                    valueString = splitString[i] + (i > 1 && i < splitString.Length ? " " : "");
                }
            }
            //thanks xml for not letting me use < or > in attributes :(
            string op = splitString[0];
            switch (op)
            {
                case "e":
                case "eq":
                case "equals":
                    Operator = OperatorType.Equals;
                    break;
                case "ne":
                case "neq":
                case "notequals":
                case "!":
                case "!e":
                case "!eq":
                case "!equals":
                    Operator = OperatorType.NotEquals;
                    break;
                case "gt":
                case "greaterthan":
                    Operator = OperatorType.GreaterThan;
                    break;
                case "lt":
                case "lessthan":
                    Operator = OperatorType.LessThan;
                    break;
                case "gte":
                case "gteq":
                case "greaterthanequals":
                    Operator = OperatorType.GreaterThanEquals;
                    break;
                case "lte":
                case "lteq":
                case "lessthanequals":
                    Operator = OperatorType.LessThanEquals;
                    break;
                default:
                    if (op != "==" && op != "!=" && op != ">" && op != "<" && op != ">=" && op != "<=") //Didn't use escape strings or anything
                    {
                        valueString = attributeValueString; //We probably don't even have an operator
                    }
                    break;
            }

            TargetItemComponentName = attribute.Parent.GetAttributeString("targetitemcomponent", "");

            foreach (XElement subElement in attribute.Parent.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "cancel":
                    case "canceleffect":
                    case "cancelstatuseffect":
                        //This only works if there's a conditional checking for status effect tags. There is no way to cancel *all* status effects atm.
                        cancelStatusEffect = 1;
                        if (subElement.GetAttributeBool("all", false)) cancelStatusEffect = 2;
                        break;
                }
            }

            if (!Enum.TryParse(AttributeName, true, out Type))
            {
                if (afflictionNames.Any(n => n == AttributeName))
                {
                    Type = ConditionType.Affliction;
                }
                else
                {
                    Type = ConditionType.PropertyValue;
                }
            }
            
            AttributeValue = valueString;
            if (float.TryParse(AttributeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                FloatValue = value;
            }
        }

        public bool Matches(ISerializableEntity target)
        {
            string valStr = AttributeValue.ToString();

            switch (Type)
            {
                case ConditionType.PropertyValue:
                    SerializableProperty property;
                    if (target.SerializableProperties.TryGetValue(AttributeName, out property))
                    {
                        return Matches(property);
                    }
                    return false;
                case ConditionType.Name:
                    return (Operator == OperatorType.Equals) == (target.Name == valStr);
                case ConditionType.HasTag:
                    {
                        string[] readTags = valStr.Split(',');
                        int matches = 0;
                        foreach (string tag in readTags)
                            if (((Item)target).HasTag(tag)) matches++;

                        //If operator is == then it needs to match everything, otherwise if its != there must be zero matches.
                        return Operator == OperatorType.Equals ? matches >= readTags.Length : matches <= 0;
                    }
                case ConditionType.HasStatusTag:
                    List<DurationListElement> durations = StatusEffect.DurationList.FindAll(d => d.Targets.Contains(target));
                    List<DelayedListElement> delays = DelayedEffect.DelayList.FindAll(d => d.Targets.Contains(target));

                    bool success = false;
                    if (durations.Count > 0 || delays.Count > 0)
                    {
                        string[] readTags = valStr.Split(',');
                        foreach (DurationListElement duration in durations)
                        {
                            int matches = 0;
                            foreach (string tag in readTags)
                                if (duration.Parent.HasTag(tag)) matches++;

                            success = Operator == OperatorType.Equals ? matches >= readTags.Length : matches <= 0;
                            if (cancelStatusEffect > 0 && success)
                                StatusEffect.DurationList.Remove(duration);
                            if (cancelStatusEffect != 2) //cancelStatusEffect 1 = only cancel once, cancelStatusEffect 2 = cancel all of matching tags
                                return success;
                        }
                        foreach (DelayedListElement delay in delays)
                        {
                            int matches = 0;
                            foreach (string tag in readTags)
                                if (delay.Parent.HasTag(tag)) matches++;

                            success = Operator == OperatorType.Equals ? matches >= readTags.Length : matches <= 0;
                            if (cancelStatusEffect > 0 && success)
                                DelayedEffect.DelayList.Remove(delay);
                            if (cancelStatusEffect != 2) //ditto
                                return success;
                        }
                    }
                    return success;
                case ConditionType.SpeciesName:
                    Character targetCharacter = target as Character;
                    if (targetCharacter == null) return false;
                    return (Operator == OperatorType.Equals) == (targetCharacter.SpeciesName == valStr);
                case ConditionType.Affliction:
                    if (target is Character targetChar)
                    {
                        var affliction = targetChar.CharacterHealth.GetAffliction(AttributeName);
                        if (affliction == null) { return false; }
                        if (FloatValue.HasValue)
                        {
                            float value = FloatValue.Value;
                            switch (Operator)
                            {
                                case OperatorType.Equals:
                                    return affliction.Strength == value;
                                case OperatorType.GreaterThan:
                                    return affliction.Strength > value;
                                case OperatorType.GreaterThanEquals:
                                    return affliction.Strength >= value;
                                case OperatorType.LessThan:
                                    return affliction.Strength < value;
                                case OperatorType.LessThanEquals:
                                    return affliction.Strength <= value;
                                case OperatorType.NotEquals:
                                    return affliction.Strength != value;
                            }
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }
        
        // TODO: refactor and add tests
        private bool Matches(SerializableProperty property)
        {
            object propertyValue = property.GetValue();

            if (propertyValue == null)
            {
                DebugConsole.ThrowError("Couldn't compare " + AttributeValue.ToString() + " (" + AttributeValue.GetType() + ") to property \"" + property.Name + "\" - property.GetValue() returns null!");
                return false;
            }

            Type type = propertyValue.GetType();
            float? floatProperty = null;
            if (type == typeof(float) || type == typeof(int))
            {
                floatProperty = (float)propertyValue;
            }

            switch (Operator)
            {
                case OperatorType.Equals:
                    if (type == typeof(bool))
                    {
                        return ((bool)propertyValue) == (AttributeValue == "true");
                    }
                    else if (FloatValue == null)
                    {
                        return propertyValue.ToString().Equals(AttributeValue);
                    }
                    else
                    {
                        return propertyValue.Equals(FloatValue);
                    }
                case OperatorType.NotEquals:
                    if (type == typeof(bool))
                    {
                        return ((bool)propertyValue) != (AttributeValue == "true");
                    }
                    else if (FloatValue == null)
                    {
                        return !propertyValue.ToString().Equals(AttributeValue);
                    }
                    else
                    {
                        return !propertyValue.Equals(FloatValue);
                    }
                case OperatorType.GreaterThan:
                    if (FloatValue == null)
                    {
                        DebugConsole.ThrowError("Couldn't compare " + AttributeValue.ToString() + " (" + AttributeValue.GetType() + ") to property \"" + property.Name + "\" (" + type + ")! "
                            + "Make sure the type of the value set in the config files matches the type of the property.");
                    }
                    else if (floatProperty > FloatValue)
                    {
                        return true;
                    }
                    break;
                case OperatorType.LessThan:
                    if (FloatValue == null)
                    {
                        DebugConsole.ThrowError("Couldn't compare " + AttributeValue.ToString() + " (" + AttributeValue.GetType() + ") to property \"" + property.Name + "\" (" + type + ")! "
                            + "Make sure the type of the value set in the config files matches the type of the property.");
                    }
                    else if (floatProperty < FloatValue)
                    {
                        return true;
                    }
                    break;
                case OperatorType.GreaterThanEquals:
                    if (FloatValue == null)
                    {
                        DebugConsole.ThrowError("Couldn't compare " + AttributeValue.ToString() + " (" + AttributeValue.GetType() + ") to property \"" + property.Name + "\" (" + type + ")! "
                            + "Make sure the type of the value set in the config files matches the type of the property.");
                    }
                    else if (floatProperty >= FloatValue)
                    {
                        return true;
                    }
                    break;
                case OperatorType.LessThanEquals:
                    if (FloatValue == null)
                    {
                        DebugConsole.ThrowError("Couldn't compare " + AttributeValue.ToString() + " (" + AttributeValue.GetType() + ") to property \"" + property.Name + "\" (" + type + ")! "
                            + "Make sure the type of the value set in the config files matches the type of the property.");
                    }
                    else if (floatProperty <= FloatValue)
                    {
                        return true;
                    }
                    break;
            }
            return false;
        }
    }

}

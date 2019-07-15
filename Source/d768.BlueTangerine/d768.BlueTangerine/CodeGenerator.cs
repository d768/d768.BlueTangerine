using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using d768.BlueTangerine.Infrastructure.Extensions;
using NetTopologySuite.Geometries;

namespace d768.BlueTangerine
{
    public class CodeGenerator
    {
        private IEnumerable<object> _objectsToWalk;

        public CodeGenerator(IEnumerable<object> objectsToWalk)
        {
            _objectsToWalk = objectsToWalk;
        }
        
        public CodeGenerator(object objectToWalk): this(new [] { objectToWalk })
        {
        }

        public string Generate()
        {
            return GenerateInternal(_objectsToWalk);
        }
        
        private string GenerateInternal(IEnumerable<Object> objects)
        {

            var alreadyWalked = new List<object>();
            var sb = new StringBuilder(1024);

            sb.Append("public IEnumerable<object> Generate()");
            sb.Append("{");
            foreach (var obj in objects)
            {
                sb.Append("yield return ");
                sb = WalkObject(obj, sb, alreadyWalked);
                sb.Append(";");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private StringBuilder WalkObject(Object obj, StringBuilder sb, 
            List<object> alreadyWalked = null)
        {
            sb.AppendLine();
            alreadyWalked = alreadyWalked ?? new List<object>();
            alreadyWalked.Add(obj);
            
            var properties = obj
                .GetType()
                .GetProperties()
                .Where(x => x.GetIndexParameters().IsNullOrEmpty())
                .Where(x => x.CanWrite && x.CanRead);

            var type = obj.GetType();
            sb.Append("new " + type.Name + " {");

            bool appendComma = false;
            DateTime workDt;
            foreach (var property in properties)
            {
                if (appendComma) sb.Append(", ");
                appendComma = true;

                var pt = property.PropertyType;
                var name = pt.Name;

                var isCollection = property
                    .PropertyType
                    .GetInterfaces()
                    .Append(property.PropertyType)
                    .Select(x => x.ToString())
                    .Any(x => 
                        x.Contains(nameof(ICollection)));

                if (isCollection)
                {
                    var enumerable = (IEnumerable) property.GetValue(obj, null);
                    var listTypeName = property.PropertyType.GetGenericArguments()[0].Name;

                    if (enumerable != null)
                    {
                        sb.Append(property.Name + $" = new List<{listTypeName}>() {{");
                        sb = WalkEnumerable(enumerable, sb, alreadyWalked);
                        sb.Append("}");
                    }
                    else
                    {
                        sb.Append(property.Name + $" = new List<{listTypeName}>()");
                    }
                }
                else if (property.PropertyType.IsEnum)
                {
                    sb.AppendFormat("{0} = {1}", property.Name, property.GetValue(obj));
                }
                else
                {
                    object value;
                    
                    value = property.GetValue(obj);
                    var isNullable = pt.IsGenericType &&
                                     pt.GetGenericTypeDefinition() == typeof(Nullable<>);
                    if (isNullable)
                    {
                        name = pt.GetGenericArguments()[0].Name;
                        if (property.GetValue(obj) == null)
                        {
                            appendComma = false;
                            continue;
                        }
                    }
                    else
                    {
                        if (property.PropertyType.IsDefaultValue(property.GetValue(obj)))
                        {
                            appendComma = false;
                            continue;
                        }
                    }

                    switch (name)
                    {
                        case "Int64":
                        case "Int32":
                        case "Int16":
                        case "Float":
                            sb.AppendFormat("{0} = {1}", property.Name, value);
                            break;
                        case "Double":
                            
                            if (double.IsNaN((double)value))
                            {
                                sb.AppendFormat("{0} = double.NaN", property.Name);
                            }
                            else
                            {
                                sb.AppendFormat("{0} = {1}", property.Name, value);
                            }
                            break;
                        case "Decimal":
                            sb.AppendFormat("{0} = {1}m", property.Name, value);
                            break;
                        case "Boolean":
                            sb.AppendFormat("{0} = {1}", property.Name,
                                Convert.ToBoolean(value) == true ? "true" : "false");
                            break;
                        case "DateTime":
                            workDt = Convert.ToDateTime(value);
                            sb.AppendFormat("{0} = new DateTime({1},{2},{3},{4},{5},{6})",
                                property.Name, workDt.Year, workDt.Month, workDt.Day, workDt.Hour,
                                workDt.Minute, workDt.Second);
                            break;
                        case "DateTimeOffset":
                            var dateTimeOffset = (DateTimeOffset) value;
                            sb.AppendFormat(
                                "{0} = new DateTimeOffset({1},{2},{3},{4},{5},{6}, TimeSpan.FromTicks({7}))",
                                property.Name, dateTimeOffset.Year, dateTimeOffset.Month,
                                dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute,
                                dateTimeOffset.Second, dateTimeOffset.Offset.Ticks);
                            break;
                        case "Guid":
                            sb.AppendFormat("{0} = Guid.Parse(\"{1}\")", property.Name,
                                value.ToString());
                            break;
                        case "String":
                            sb.AppendFormat("{0} = \"{1}\"", property.Name, value);
                            break;
                        case "TimeSpan":
                            var span = (TimeSpan) value;
                            sb.AppendFormat("{0} = TimeSpan.FromTicks({1})", 
                                property.Name, span.Ticks);
                            break;
                        case "Point":
                            var point = (Point)value;
                            sb.AppendFormat("{0} = new Point({1},{2},{3})", property.Name,
                               !double.IsNaN(point.X) ? point.X.ToString() : "double.NaN",
                               !double.IsNaN(point.Y) ? point.Y.ToString() : "double.NaN",
                               !double.IsNaN(point.Z) ? point.Z.ToString() : "double.NaN");
                            break;
                        default:
                            // Handles all user classes, should likely have a better way
                            // to detect user class
                            var objToWalk = property.GetValue(obj);
                            
                            if (!alreadyWalked.Contains(objToWalk))
                            {
                                sb.AppendFormat("{0} = ", property.Name);
                                WalkObject(objToWalk, sb, alreadyWalked);
                            }
                            else
                            {
                                appendComma = false;
                            }
                            break;
                    }
                }
            }

            sb.Append("}");

            return sb;
        }

        private StringBuilder WalkEnumerable(IEnumerable list, StringBuilder sb, 
            List<object> alreadyWalked)
        {
            bool appendComma = false;
            foreach (object obj in list)
            {
                if(alreadyWalked.Contains(obj))
                    continue;
                
                if (appendComma) sb.Append(", ");
                appendComma = true;
                WalkObject(obj, sb, alreadyWalked);
            }

            return sb;
        }
    }
}
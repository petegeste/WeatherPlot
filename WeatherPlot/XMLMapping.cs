using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;

namespace WeatherPlot
{

    #region Attributes
    /// <summary>
    /// Attribute denotes a class that may be serialized and deserialized to an XML
    /// </summary>
    public class Mappable : Attribute
    {
        public string Name;
        /// <summary>
        /// Denotes that this class models an XElement
        /// </summary>
        /// <param name="name">The name the XElement this object represents</param>
        public Mappable(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Denotes a Mappable that may take different names, but retains the same structure
        /// Name is inherited from member ChildMapping, and only ChildMappings may be used
        /// </summary>
        public Mappable()
        {

        }
    }

    /// <summary>
    /// Attribute denotes a property or field of a class that may be serialized/deserialized to an XML
    /// </summary>
    public class Mapping : Attribute
    {
        public string Name;
        public bool Optional;
        /// <summary>
        /// Denotes that this field or property is mapped to an XElement
        /// </summary>
        /// <param name="name">Mapped attribute's name</param>
        /// <param name="optional">Choose whether absence of this property results in exception</param>
        public Mapping(string name, bool optional = true)
        {
            Name = name;
            Optional = optional;
        }
    }


    /// <summary>
    /// Attribute denotes a property or field of a class that may be serialized/deserialized to an XML.
    /// - If initialized with type, the member type must be a list.
    /// - If initialized with a string, member will map to an XElement child by the same name.
    /// In this case, the member type must be a Mappable
    /// </summary>
    public class ChildMapping : Attribute
    {
        public bool Optional;
        public string Name;
        public Type ChildType;
        public ChildMapping(Type childType)
        {
            ChildType = childType;
        }
        public ChildMapping(string name, bool optional = true)
        {
            Name = name;
            Optional = optional;
        }
    }

    /// <summary>
    /// Attribute denotes a property to be mapped to the value of the XElement
    /// </summary>
    public class ValueMapping : Attribute
    {

    }

    /// <summary>
    /// Attribute denotes that a property maps to a child element that contains a value and is not a Mappable
    /// </summary>
    public class ValueChildMapping : Attribute
    {
        public string Name;
        public bool Optional;
        public ValueChildMapping(string name, bool optional = true)
        {
            Name = name;
            Optional = optional;
        }
    }

    /// <summary>
    /// Attribute denotes a simple XElement (without attributes) that is used to list mappable XElements
    /// </summary>
    public class ListMapping : Attribute
    {
        public string Name;
        public Type ChildType;
        public bool Optional;
        public ListMapping(string name, Type childType, bool optional = true)
        {
            Name = name;
            ChildType = childType;
            Optional = optional;
        }
    }

    #endregion

    /// <summary>
    /// Provides methods to convert serializable classes to and from XML using mappings
    /// </summary>
    public static class Composition
    {
        public enum DeserializeCaution
        {
            None,
            CheckElementName
        }

        /// <summary>
        /// Serializes a class into an XElement using the Mappable/Mapping attributes.
        /// Only public properties may be serialized
        /// </summary>
        /// <param name="serializable"></param>
        /// <returns></returns>
        public static XElement Serialize(object serializable, string rootElementName = null)
        {
            if (serializable == null)
            {
                return null;
            }

            Type t = serializable.GetType();
            Attribute[] attrs = Attribute.GetCustomAttributes(t);
            Mappable l = (Mappable)attrs.FirstOrDefault(a => a is Mappable);
            if (l == null)
            {
                return null;
            }

            if (rootElementName != null)
            {
                l.Name = rootElementName;
            }

            // make the element
            XElement elm = new XElement(l.Name);

            var members = ((object[])t.GetFields()).Union(t.GetProperties());

            // assign the attributes
            foreach (var member in members.Where(m => GetCustomAttribute(m, typeof(Mapping)) != null))
            {
                Mapping trait = (Mapping)GetCustomAttribute(member, typeof(Mapping));
                MappingConversion conversion = (MappingConversion)GetCustomAttribute(member, typeof(MappingConversion));
                object value = ConvertOut(member, serializable);

                if (value == null && trait.Optional)
                {
                    continue;
                }

                XAttribute attr = new XAttribute(trait.Name, value ?? "");
                elm.Add(attr);
            }

            // assign values to simple lists
            var lists = members.Where(m => GetCustomAttribute(m, typeof(ListMapping)) != null);
            foreach (var listMember in lists)
            {
                ListMapping listMapping = (ListMapping)GetCustomAttribute(listMember, typeof(ListMapping));
                IList children = GetValue(listMember, serializable) as IList;
                if (children == null)
                {
                    throw new MappableException($"List '{listMapping.Name}' cannot be serialized: is not collection or is null");
                }
                XElement listElm = new XElement(listMapping.Name);
                foreach (var child in children)
                {
                    listElm.Add(Serialize(child));
                }
                elm.Add(listElm);
            }

            // assign a value to the XElement
            var valueMapping = members.FirstOrDefault(m => GetCustomAttribute(m, typeof(ValueMapping)) != null);
            if (valueMapping != null)
            {
                string val = ConvertOut(valueMapping, serializable);
                if (val != null)
                {
                    elm.Value = val;
                }
            }

            // assign the children
            foreach (var member in members.Where(m => GetCustomAttribute(m, typeof(ChildMapping)) != null))
            {
                ChildMapping mapping = (ChildMapping)GetCustomAttribute(member, typeof(ChildMapping));
                if (mapping.ChildType == null)
                {
                    Mappable childMappable = (Mappable)MemberType(member).GetCustomAttribute(typeof(Mappable));

                    // maps only one value
                    XElement childElm = Serialize(GetValue(member, serializable), rootElementName: childMappable.Name == null ? mapping.Name : null);
                    if (childElm != null)
                    { elm.Add(childElm); }
                }
                else
                {
                    // mapped to many values
                    IList children = GetValue(member, serializable) as IList;
                    if (children == null)
                    {
                        return null;
                    }

                    foreach (var child in children)
                    {
                        XElement childElm = Serialize(child);
                        elm.Add(childElm);
                    }
                }


            }

            // assign simple value children (non-Mappable children mapped to members of model)
            foreach (var member in members.Where(m => GetCustomAttribute(m, typeof(ValueChildMapping)) != null))
            {
                ValueChildMapping mapping = (ValueChildMapping)GetCustomAttribute(member, typeof(ValueChildMapping));
                XElement simpleChild = new XElement(mapping.Name);
                string val = ConvertOut(member, serializable);

                if (val != null)
                {
                    simpleChild.Value = val;
                    elm.Add(simpleChild);
                }
            }


            return elm;
        }

        /// <summary>
        /// Convert an XElement to the assigned type using mappings provided by Mappable/Mapping attributes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        public static bool TryDeserialize<T>(XElement element, T obj, DeserializeCaution caution = DeserializeCaution.CheckElementName, string rootMappableName = null)
        {

            Type type = obj.GetType();
            Mappable mappable = type.GetCustomAttribute(typeof(Mappable)) as Mappable;

            if (mappable == null)
            {
                throw new MappableException("Object type is not Mappable");
            }

            if (rootMappableName != null)
            {
                mappable.Name = rootMappableName;
            }

            if ((element.Name.LocalName != mappable.Name) && (caution == DeserializeCaution.CheckElementName))
            {
                return false;
            }

            var members = ((object[])type.GetFields()).Union(type.GetProperties());


            var fields = type.GetFields().Where(f => f.GetCustomAttribute(typeof(Mapping)) != null);
            var properties = type.GetProperties().Where(p => p.GetCustomAttribute(typeof(Mapping)) != null);

            // Assign members
            foreach (var member in members.Where(m => GetCustomAttribute(m, typeof(Mapping)) != null))
            {
                Mapping mapping = (Mapping)GetCustomAttribute(member, typeof(Mapping));

                string value = element.Attribute(mapping.Name)?.Value;
                if (value == null)
                {
                    if (mapping.Optional == false)
                    {
                        throw new MappableException($"Mapped property {mapping.Name} was not found in XElement '{element}'");
                    }
                }
                else
                {
                    object convertedValue = null;
                    try
                    {
                        convertedValue = ConvertIn(member, element.Attribute(mapping.Name)?.Value);
                    }
                    catch
                    {
                        convertedValue = default;
                    }
                    SetValue(member, obj, convertedValue);
                }

            }

            // get simple lists
            var lists = members.Where(m => GetCustomAttribute(m, typeof(ListMapping)) != null);
            foreach (var listMember in lists)
            {
                ListMapping listMapping = (ListMapping)GetCustomAttribute(listMember, typeof(ListMapping));
                Mappable childMappable = (Mappable)listMapping.ChildType.GetCustomAttribute(typeof(Mappable));
                if (childMappable == null)
                {
                    throw new MappableException("Children of ListMappings should be Mappables");
                }

                IList children = GetValue(listMember, obj) as IList;
                if (children == null)
                {
                    throw new MappableException($"List '{listMapping.Name}' cannot be serialized: is not collection or is not initialized");
                }
                XElement listElm = element.Descendants().FirstOrDefault(e => e.Name.LocalName == listMapping.Name);
                if (listElm == null)
                {
                    if (!listMapping.Optional)
                    {
                        throw new MappableException("Required list is missing");
                    }
                    else
                    {
                        continue;
                    }
                }
                foreach (var child in listElm?.XPathSelectElements(childMappable.Name) ?? new List<XElement>())
                {
                    object newChil = Activator.CreateInstance(listMapping.ChildType);
                    if (!TryDeserialize(child, newChil, caution))
                    {
                        throw new MappableException($"Could not assign value from element {listElm}");
                    }

                    children.Add(newChil);
                }
            }

            // assign ValueMapping
            var valueMapping = members.FirstOrDefault(m => GetCustomAttribute(m, typeof(ValueMapping)) != null);
            if (valueMapping != null)
            {
                SetValue(valueMapping, obj, ConvertIn(valueMapping, element.Value));
            }

            // Assign children
            var childMembers = members.Where(m => GetCustomAttribute(m, typeof(ChildMapping)) != null);
            foreach (var member in childMembers)
            {
                ChildMapping mapping = (ChildMapping)GetCustomAttribute(member, typeof(ChildMapping));

                if (mapping.ChildType == null)
                {
                    // maps only one value
                    object childObj = Activator.CreateInstance(MemberType(member));

                    // rename mapping if necessary
                    bool overrideMappableName = false;
                    Mappable childObjMappable = (Mappable)childObj.GetType().GetCustomAttribute(typeof(Mappable));
                    if (childObjMappable != null && childObjMappable.Name == null)
                    { overrideMappableName = true; }


                    XElement child = element.Descendants(mapping.Name).FirstOrDefault();
                    if (child == null)
                    {
                        if (!mapping.Optional)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    if (!TryDeserialize(child, childObj, caution, rootMappableName: overrideMappableName ? mapping.Name : null))
                    {
                        return false;
                    }
                    SetValue(member, obj, childObj);
                }
                else
                {
                    // maps many values into a list
                    Mappable childTypeMapping = (Mappable)mapping.ChildType.GetCustomAttribute(typeof(Mappable));
                    IList children = GetValue(member, obj) as IList;
                    if (children == null)
                    {
                        throw new MappableException($"Object member {member} is not initilaized or is not a collection");
                    }
                    IEnumerable<XElement> elmChildren = element.Descendants().Where(elm => elm.Name.LocalName == childTypeMapping.Name);
                    foreach (var child in elmChildren)
                    {
                        object childObj = Activator.CreateInstance(mapping.ChildType);
                        if (!TryDeserialize(child, childObj, caution))
                        {
                            return false;
                        }
                        children.Add(childObj);
                    }
                }
            }

            // assign simple value children (non-Mappable children mapped to members of model)
            foreach (var member in members.Where(m => GetCustomAttribute(m, typeof(ValueChildMapping)) != null))
            {
                ValueChildMapping mapping = (ValueChildMapping)GetCustomAttribute(member, typeof(ValueChildMapping));
                XElement prop = element.Descendants(mapping.Name).FirstOrDefault();
                if (prop == null)
                {
                    if (!mapping.Optional)
                    {
                        throw new MappableException($"Missing required property '{mapping.Name}' from XElement '{prop}'");
                    }
                    else
                    {
                        continue;
                    }
                }
                SetValue(member, obj, ConvertIn(member, prop.Value));
            }

            return true;
        }

        #region Helper Methods

        /// <summary>
        /// Gets the field or property type for this member
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private static Type MemberType(object info)
        {
            if (info is FieldInfo)
            {
                return (info as FieldInfo).FieldType;
            }
            else
            {
                return (info as PropertyInfo).PropertyType;
            }
        }

        /// <summary>
        /// Converts a member value to a string output, using MapConversion if available
        /// </summary>
        /// <param name="member">The class member whose value is to be converted</param>
        /// <param name="instance">The instance of the object with the given member</param>
        /// <returns></returns>
        private static string ConvertOut(object member, object instance)
        {
            try
            {
                MappingConversion mapConversion = (MappingConversion)GetCustomAttribute(member, typeof(MappingConversion));
                if (mapConversion != null)
                {
                    return mapConversion.OutConversion(GetValue(member, instance));
                }
                else
                {
                    return GetValue(member, instance)?.ToString();
                }
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// Converts a string to a value output using the MapCOnversion if it is available
        /// </summary>
        /// <param name="member">The class member whose value is to be converted</param>
        /// <param name="value">The string value to convert to the desired out type</param>
        /// <returns></returns>
        private static object ConvertIn(object member, string value)
        {
            MappingConversion mapConversion = (MappingConversion)GetCustomAttribute(member, typeof(MappingConversion));
            if (mapConversion != null)
            {
                return mapConversion.InConversion(value);
            }
            else
            {
                try
                {
                    return Convert.ChangeType(value, MemberType(member));
                }
                catch
                {
                    throw new MappableException($"Cannot automatically convert string '{value}' to type {MemberType(member)}. Consider using a value formatter.");
                }
            }
        }

        /// <summary>
        /// Obtains a custom attribute
        /// </summary>
        /// <param name="info"></param>
        /// <param name="attributeType"></param>
        /// <returns></returns>
        private static Attribute GetCustomAttribute(object info, Type attributeType)
        {
            if (info is FieldInfo)
            {
                return (info as FieldInfo).GetCustomAttribute(attributeType);
            }
            else
            {
                return (info as PropertyInfo).GetCustomAttribute(attributeType);
            }
        }

        /// <summary>
        /// Gets the field/property value
        /// </summary>
        /// <param name="info"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static object GetValue(object info, object obj)
        {
            if (info is FieldInfo)
            {
                return (info as FieldInfo).GetValue(obj);
            }
            else
            {
                return (info as PropertyInfo).GetValue(obj);
            }
        }

        /// <summary>
        /// Sets the field/property value
        /// </summary>
        /// <param name="info"></param>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        private static void SetValue(object info, object obj, object value)
        {
            if (info is FieldInfo)
            {
                (info as FieldInfo).SetValue(obj, value);
            }
            else
            {
                (info as PropertyInfo).SetValue(obj, value);
            }
        }

        #endregion

    }

    #region Conversion

    /// <summary>
    /// Provides
    /// </summary>
    public abstract class MappingConversion : Attribute
    {
        public abstract string OutConversion(object input);
        public abstract object InConversion(string input);
    }

    public class DateTimeFormatter : MappingConversion
    {
        private string FormatString;
        public override object InConversion(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            return DateTime.Parse(input);
        }

        public override string OutConversion(object input)
        {
            if ((DateTime)input == default)
            {
                return "";
            }

            return ((DateTime)input).ToString(FormatString);
        }
        /// <summary>
        /// Constructs the Conversion attribute.
        /// </summary>
        /// <param name="format">Format to use for DateTime. If null, uses TAE default timestamp format</param>
        public DateTimeFormatter(string format)
        {
            FormatString = format;
        }
    }

    /// <summary>
    /// Converts between an enum and an input string
    /// </summary>
    public class EnumConversion : MappingConversion
    {
        Type EnumType;
        public EnumConversion(Type enumType)
        {
            EnumType = enumType;
        }
        public override object InConversion(string input)
        {
            try
            {
                return Enum.Parse(EnumType, input, true);
            }
            catch
            {
                return null;
            }

        }
        public override string OutConversion(object input)
        {
            return Enum.GetName(EnumType, input);
        }
    }

    /// <summary>
    /// Converts a byte array to a base64
    /// </summary>
    public class Base64Conversion : MappingConversion
    {
        public override object InConversion(string input)
        {
            if (input == null)
            {
                return null;
            }

            return Convert.FromBase64String(input);
        }
        public override string OutConversion(object input)
        {
            if (input == null)
            {
                return null;
            }

            return Convert.ToBase64String(input as byte[]);
        }
    }
    #endregion

    #region Exceptions

    public class MappableException : Exception
    {
        public MappableException() : base()
        {

        }

        public MappableException(string message) : base(message)
        {

        }

        public MappableException(string message, Exception inner) : base(message, inner)
        {

        }
    }

    #endregion
}

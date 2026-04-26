using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Router.Wpf.Core
{
    public interface IToken
    {

    }


    public class StringToken : IToken
    {
        public StringToken(string value)
        {
            Value = value;
        }
        public string Value { get; set; }
    }

    public class DynamicToken : IToken
    {
        private readonly static Dictionary<string, Type> TypeMapping = new(){
            {"?", typeof(Nullable) },
            {":int", typeof(int)},
            {":string", typeof(string)},
            {":float", typeof(float)},
            {":double", typeof(double)},
            {":long", typeof(long)},
            {":short", typeof(short)},
            {":byte", typeof(byte)},
            {":char", typeof(char)},
        };

        public DynamicToken(string name, string type)
        {
            Name = name;
            Type = TypeMapping.TryGetValue(type, out var t)
                ? t
                : throw new ArgumentException($"Unknown type tag '{type}'. Supported tags: {string.Join(", ", TypeMapping.Keys)}.", nameof(type));
            TypeTag = type;
        }


        public string Name { get; set; }
        public Type Type { get; set; }
        public string TypeTag { get; set; }

        public object GetValue(string value)
        {
            if (Type == typeof(Nullable))
                return value;
            return Convert.ChangeType(value, Type);
        }
    }

    public class MixedToken : IToken
    {
        public MixedToken(List<IToken> tokens)
        {
            Tokens = tokens;
        }
        public List<IToken> Tokens { get; set; }
    }
}

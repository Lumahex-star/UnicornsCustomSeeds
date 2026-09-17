using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace UnicornsCustomSeeds.TemplateUtils
{
    /// <summary>
    /// Newtonsoft.Json's default ExpressionValueProvider JIT-compiles property
    /// accessors via System.Reflection.Emit/LambdaExpression.Compile the first time
    /// each type is (de)serialized. Some IL2CPP installs cannot do dynamic codegen and
    /// throw a FileLoadException from DynamicMethod.CreateDelegate on that first call,
    /// breaking every JSON load/save in the mod. Plain PropertyInfo/FieldInfo reflection
    /// has no such requirement and works identically on every runtime.
    /// </summary>
    internal sealed class SafeReflectionValueProvider : IValueProvider
    {
        private readonly PropertyInfo _property;
        private readonly FieldInfo _field;

        public SafeReflectionValueProvider(MemberInfo member)
        {
            _property = member as PropertyInfo;
            _field = member as FieldInfo;
        }

        public object GetValue(object target)
        {
            if (_property != null) return _property.GetValue(target, null);
            if (_field != null) return _field.GetValue(target);
            return null;
        }

        public void SetValue(object target, object value)
        {
            if (_property != null) _property.SetValue(target, value, null);
            else if (_field != null) _field.SetValue(target, value);
        }
    }

    /// <summary>
    /// Applied mod-wide via JsonConvert.DefaultSettings in Core.OnInitializeMelon so
    /// every JsonConvert.SerializeObject/DeserializeObject call in the mod — none of
    /// which pass explicit settings — picks it up automatically.
    /// </summary>
    internal sealed class SafeContractResolver : DefaultContractResolver
    {
        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            return new SafeReflectionValueProvider(member);
        }
    }
}

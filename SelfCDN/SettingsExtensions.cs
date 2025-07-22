using System.Reflection;

namespace SelfCDN
{
    public static class SettingsExtensions
    {
        public static void ApplyDefaults<T>(this T target) where T : class, new()
        {
            var defaults = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite)
                {
                    continue;
                };

                var currentValue = prop.GetValue(target);
                var defaultValue = prop.GetValue(defaults);

                if (currentValue == null && defaultValue != null)
                {
                    prop.SetValue(target, defaultValue);
                }
            }
        }
    }
}

using System.Globalization;

namespace Trecs
{
    static class SerializationWriterScopeExtensions
    {
        public static void PushScope(this ISerializationWriter writer, string name)
        {
            if (writer is FlatPathSerializationWriter fpw)
                fpw.PushScope(name);
        }

        public static void PushScope<T0>(this ISerializationWriter writer, string format, T0 arg0)
        {
            if (writer is FlatPathSerializationWriter fpw)
                fpw.PushScope(string.Format(CultureInfo.InvariantCulture, format, arg0));
        }

        public static void PushScope<T0, T1>(
            this ISerializationWriter writer,
            string format,
            T0 arg0,
            T1 arg1
        )
        {
            if (writer is FlatPathSerializationWriter fpw)
                fpw.PushScope(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
        }

        public static void PopScope(this ISerializationWriter writer)
        {
            if (writer is FlatPathSerializationWriter fpw)
                fpw.PopScope();
        }
    }
}

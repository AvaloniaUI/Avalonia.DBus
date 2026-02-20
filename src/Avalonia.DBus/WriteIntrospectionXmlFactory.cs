using System.Text;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
delegate void WriteIntrospectionXmlFactory(StringBuilder sb, string indent);

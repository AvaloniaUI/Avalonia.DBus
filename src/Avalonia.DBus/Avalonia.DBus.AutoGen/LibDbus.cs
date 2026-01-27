using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen
{
    internal static unsafe partial class LibDbus
    {
        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_error_init(DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_error_free(DBusError* error);

        [DllImport("libdbus-1.so.3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void dbus_set_error(DBusError* error, [NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* message, __arglist);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_set_error_const(DBusError* error, [NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_move_error(DBusError* src, DBusError* dest);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_error_has_name([NativeTypeName("const DBusError *")] DBusError* error, [NativeTypeName("const char *")] byte* name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_error_is_set([NativeTypeName("const DBusError *")] DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_parse_address([NativeTypeName("const char *")] byte* address, DBusAddressEntry*** entry_result, int* array_len, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_address_entry_get_value(DBusAddressEntry* entry, [NativeTypeName("const char *")] byte* key);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_address_entry_get_method(DBusAddressEntry* entry);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_address_entries_free(DBusAddressEntry** entries);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_address_escape_value([NativeTypeName("const char *")] byte* value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_address_unescape_value([NativeTypeName("const char *")] byte* value, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_new(int message_type);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_new_method_call([NativeTypeName("const char *")] byte* bus_name, [NativeTypeName("const char *")] byte* path, [NativeTypeName("const char *")] byte* iface, [NativeTypeName("const char *")] byte* method);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_new_method_return(DBusMessage* method_call);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_new_signal([NativeTypeName("const char *")] byte* path, [NativeTypeName("const char *")] byte* iface, [NativeTypeName("const char *")] byte* name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_new_error(DBusMessage* reply_to, [NativeTypeName("const char *")] byte* error_name, [NativeTypeName("const char *")] byte* error_message);

        [DllImport("libdbus-1.so.3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern DBusMessage* dbus_message_new_error_printf(DBusMessage* reply_to, [NativeTypeName("const char *")] byte* error_name, [NativeTypeName("const char *")] byte* error_format, __arglist);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_copy([NativeTypeName("const DBusMessage *")] DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_ref(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_unref(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_get_type(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_path(DBusMessage* message, [NativeTypeName("const char *")] byte* object_path);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_path(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_path(DBusMessage* message, [NativeTypeName("const char *")] byte* object_path);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_interface(DBusMessage* message, [NativeTypeName("const char *")] byte* iface);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_interface(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_interface(DBusMessage* message, [NativeTypeName("const char *")] byte* iface);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_member(DBusMessage* message, [NativeTypeName("const char *")] byte* member);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_member(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_member(DBusMessage* message, [NativeTypeName("const char *")] byte* member);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_error_name(DBusMessage* message, [NativeTypeName("const char *")] byte* name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_error_name(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_destination(DBusMessage* message, [NativeTypeName("const char *")] byte* destination);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_destination(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_sender(DBusMessage* message, [NativeTypeName("const char *")] byte* sender);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_sender(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_signature(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_set_no_reply(DBusMessage* message, [NativeTypeName("dbus_bool_t")] uint no_reply);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_get_no_reply(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_is_method_call(DBusMessage* message, [NativeTypeName("const char *")] byte* iface, [NativeTypeName("const char *")] byte* method);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_is_signal(DBusMessage* message, [NativeTypeName("const char *")] byte* iface, [NativeTypeName("const char *")] byte* signal_name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_is_error(DBusMessage* message, [NativeTypeName("const char *")] byte* error_name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_destination(DBusMessage* message, [NativeTypeName("const char *")] byte* bus_name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_sender(DBusMessage* message, [NativeTypeName("const char *")] byte* unique_bus_name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_has_signature(DBusMessage* message, [NativeTypeName("const char *")] byte* signature);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_uint32_t")]
        public static partial uint dbus_message_get_serial(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_set_serial(DBusMessage* message, [NativeTypeName("dbus_uint32_t")] uint serial);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_reply_serial(DBusMessage* message, [NativeTypeName("dbus_uint32_t")] uint reply_serial);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_uint32_t")]
        public static partial uint dbus_message_get_reply_serial(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_set_auto_start(DBusMessage* message, [NativeTypeName("dbus_bool_t")] uint auto_start);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_get_auto_start(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_get_path_decomposed(DBusMessage* message, [NativeTypeName("char ***")] byte*** path);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_get_container_instance(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_container_instance(DBusMessage* message, [NativeTypeName("const char *")] byte* object_path);

        [DllImport("libdbus-1.so.3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dbus_bool_t")]
        public static extern uint dbus_message_append_args(DBusMessage* message, int first_arg_type, __arglist);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_append_args_valist(DBusMessage* message, int first_arg_type, [NativeTypeName("va_list")] __va_list var_args);

        [DllImport("libdbus-1.so.3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dbus_bool_t")]
        public static extern uint dbus_message_get_args(DBusMessage* message, DBusError* error, int first_arg_type, __arglist);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_get_args_valist(DBusMessage* message, DBusError* error, int first_arg_type, [NativeTypeName("va_list")] __va_list var_args);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_contains_unix_fds(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_init_closed(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_init(DBusMessage* message, DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_has_next(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_next(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_message_iter_get_signature(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_iter_get_arg_type(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_iter_get_element_type(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_recurse(DBusMessageIter* iter, DBusMessageIter* sub);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_get_basic(DBusMessageIter* iter, void* value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_iter_get_element_count(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [Obsolete]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_iter_get_array_len(DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_get_fixed_array(DBusMessageIter* iter, void* value, int* n_elements);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_init_append(DBusMessage* message, DBusMessageIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_append_basic(DBusMessageIter* iter, int type, [NativeTypeName("const void *")] void* value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_append_fixed_array(DBusMessageIter* iter, int element_type, [NativeTypeName("const void *")] void* value, int n_elements);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_open_container(DBusMessageIter* iter, int type, [NativeTypeName("const char *")] byte* contained_signature, DBusMessageIter* sub);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_iter_close_container(DBusMessageIter* iter, DBusMessageIter* sub);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_abandon_container(DBusMessageIter* iter, DBusMessageIter* sub);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_iter_abandon_container_if_open(DBusMessageIter* iter, DBusMessageIter* sub);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_lock(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_set_error_from_message(DBusError* error, DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_allocate_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_free_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_set_data(DBusMessage* message, [NativeTypeName("dbus_int32_t")] int slot, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_func);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_message_get_data(DBusMessage* message, [NativeTypeName("dbus_int32_t")] int slot);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_type_from_string([NativeTypeName("const char *")] byte* type_str);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_message_type_to_string(int type);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_marshal(DBusMessage* msg, [NativeTypeName("char **")] byte** marshalled_data_p, int* len_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_message_demarshal([NativeTypeName("const char *")] byte* str, int len, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_message_demarshal_bytes_needed([NativeTypeName("const char *")] byte* str, int len);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_message_set_allow_interactive_authorization(DBusMessage* message, [NativeTypeName("dbus_bool_t")] uint allow);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_message_get_allow_interactive_authorization(DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusConnection* dbus_connection_open([NativeTypeName("const char *")] byte* address, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusConnection* dbus_connection_open_private([NativeTypeName("const char *")] byte* address, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusConnection* dbus_connection_ref(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_unref(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_close(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_is_connected(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_is_authenticated(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_is_anonymous(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_connection_get_server_id(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_can_send_type(DBusConnection* connection, int type);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_exit_on_disconnect(DBusConnection* connection, [NativeTypeName("dbus_bool_t")] uint exit_on_disconnect);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_flush(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_read_write_dispatch(DBusConnection* connection, int timeout_milliseconds);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_read_write(DBusConnection* connection, int timeout_milliseconds);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_connection_borrow_message(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_return_message(DBusConnection* connection, DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_steal_borrowed_message(DBusConnection* connection, DBusMessage* message);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_connection_pop_message(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusDispatchStatus dbus_connection_get_dispatch_status(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusDispatchStatus dbus_connection_dispatch(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_has_messages_to_send(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_send(DBusConnection* connection, DBusMessage* message, [NativeTypeName("dbus_uint32_t *")] uint* client_serial);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_send_with_reply(DBusConnection* connection, DBusMessage* message, DBusPendingCall** pending_return, int timeout_milliseconds);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_connection_send_with_reply_and_block(DBusConnection* connection, DBusMessage* message, int timeout_milliseconds, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_set_watch_functions(DBusConnection* connection, [NativeTypeName("DBusAddWatchFunction")] IntPtr add_function, [NativeTypeName("DBusRemoveWatchFunction")] IntPtr remove_function, [NativeTypeName("DBusWatchToggledFunction")] IntPtr toggled_function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_set_timeout_functions(DBusConnection* connection, [NativeTypeName("DBusAddTimeoutFunction")] IntPtr add_function, [NativeTypeName("DBusRemoveTimeoutFunction")] IntPtr remove_function, [NativeTypeName("DBusTimeoutToggledFunction")] IntPtr toggled_function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_wakeup_main_function(DBusConnection* connection, [NativeTypeName("DBusWakeupMainFunction")] IntPtr wakeup_main_function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_dispatch_status_function(DBusConnection* connection, [NativeTypeName("DBusDispatchStatusFunction")] IntPtr function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_unix_user(DBusConnection* connection, [NativeTypeName("unsigned long *")] UIntPtr* uid);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_unix_process_id(DBusConnection* connection, [NativeTypeName("unsigned long *")] UIntPtr* pid);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_adt_audit_session_data(DBusConnection* connection, void** data, [NativeTypeName("dbus_int32_t *")] int* data_size);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_unix_user_function(DBusConnection* connection, [NativeTypeName("DBusAllowUnixUserFunction")] IntPtr function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_windows_user(DBusConnection* connection, [NativeTypeName("char **")] byte** windows_sid_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_windows_user_function(DBusConnection* connection, [NativeTypeName("DBusAllowWindowsUserFunction")] IntPtr function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_allow_anonymous(DBusConnection* connection, [NativeTypeName("dbus_bool_t")] uint value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_route_peer_messages(DBusConnection* connection, [NativeTypeName("dbus_bool_t")] uint value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_add_filter(DBusConnection* connection, [NativeTypeName("DBusHandleMessageFunction")] IntPtr function, void* user_data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_remove_filter(DBusConnection* connection, [NativeTypeName("DBusHandleMessageFunction")] IntPtr function, void* user_data);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_allocate_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_free_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_set_data(DBusConnection* connection, [NativeTypeName("dbus_int32_t")] int slot, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_func);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_connection_get_data(DBusConnection* connection, [NativeTypeName("dbus_int32_t")] int slot);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_change_sigpipe([NativeTypeName("dbus_bool_t")] uint will_modify_sigpipe);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_max_message_size(DBusConnection* connection, [NativeTypeName("long")] IntPtr size);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_max_message_size(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_max_received_size(DBusConnection* connection, [NativeTypeName("long")] IntPtr size);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_max_received_size(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_max_message_unix_fds(DBusConnection* connection, [NativeTypeName("long")] IntPtr n);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_max_message_unix_fds(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_set_max_received_unix_fds(DBusConnection* connection, [NativeTypeName("long")] IntPtr n);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_max_received_unix_fds(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_outgoing_size(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial IntPtr dbus_connection_get_outgoing_unix_fds(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusPreallocatedSend* dbus_connection_preallocate_send(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_free_preallocated_send(DBusConnection* connection, DBusPreallocatedSend* preallocated);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_connection_send_preallocated(DBusConnection* connection, DBusPreallocatedSend* preallocated, DBusMessage* message, [NativeTypeName("dbus_uint32_t *")] uint* client_serial);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_try_register_object_path(DBusConnection* connection, [NativeTypeName("const char *")] byte* path, [NativeTypeName("const DBusObjectPathVTable *")] DBusObjectPathVTable* vtable, void* user_data, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_register_object_path(DBusConnection* connection, [NativeTypeName("const char *")] byte* path, [NativeTypeName("const DBusObjectPathVTable *")] DBusObjectPathVTable* vtable, void* user_data);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_try_register_fallback(DBusConnection* connection, [NativeTypeName("const char *")] byte* path, [NativeTypeName("const DBusObjectPathVTable *")] DBusObjectPathVTable* vtable, void* user_data, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_register_fallback(DBusConnection* connection, [NativeTypeName("const char *")] byte* path, [NativeTypeName("const DBusObjectPathVTable *")] DBusObjectPathVTable* vtable, void* user_data);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_unregister_object_path(DBusConnection* connection, [NativeTypeName("const char *")] byte* path);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_object_path_data(DBusConnection* connection, [NativeTypeName("const char *")] byte* path, void** data_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_list_registered(DBusConnection* connection, [NativeTypeName("const char *")] byte* parent_path, [NativeTypeName("char ***")] byte*** child_entries);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_unix_fd(DBusConnection* connection, int* fd);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_connection_get_socket(DBusConnection* connection, int* fd);

        [LibraryImport("libdbus-1.so.3")]
        [Obsolete]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_watch_get_fd(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_watch_get_unix_fd(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_watch_get_socket(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("unsigned int")]
        public static partial uint dbus_watch_get_flags(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_watch_get_data(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_watch_set_data(DBusWatch* watch, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_watch_handle(DBusWatch* watch, [NativeTypeName("unsigned int")] uint flags);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_watch_get_enabled(DBusWatch* watch);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_timeout_get_interval(DBusTimeout* timeout);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_timeout_get_data(DBusTimeout* timeout);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_timeout_set_data(DBusTimeout* timeout, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_timeout_handle(DBusTimeout* timeout);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_timeout_get_enabled(DBusTimeout* timeout);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusConnection* dbus_bus_get(DBusBusType type, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusConnection* dbus_bus_get_private(DBusBusType type, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_bus_register(DBusConnection* connection, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_bus_set_unique_name(DBusConnection* connection, [NativeTypeName("const char *")] byte* unique_name);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* dbus_bus_get_unique_name(DBusConnection* connection);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("unsigned long")]
        public static partial UIntPtr dbus_bus_get_unix_user(DBusConnection* connection, [NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_bus_get_id(DBusConnection* connection, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_bus_request_name(DBusConnection* connection, [NativeTypeName("const char *")] byte* name, [NativeTypeName("unsigned int")] uint flags, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_bus_release_name(DBusConnection* connection, [NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_bus_name_has_owner(DBusConnection* connection, [NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_bus_start_service_by_name(DBusConnection* connection, [NativeTypeName("const char *")] byte* name, [NativeTypeName("dbus_uint32_t")] uint flags, [NativeTypeName("dbus_uint32_t *")] uint* reply, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_bus_add_match(DBusConnection* connection, [NativeTypeName("const char *")] byte* rule, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_bus_remove_match(DBusConnection* connection, [NativeTypeName("const char *")] byte* rule, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_get_local_machine_id();

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_get_version(int* major_version_p, int* minor_version_p, int* micro_version_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_setenv([NativeTypeName("const char *")] byte* variable, [NativeTypeName("const char *")] byte* value);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_try_get_local_machine_id(DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusPendingCall* dbus_pending_call_ref(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_pending_call_unref(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_pending_call_set_notify(DBusPendingCall* pending, [NativeTypeName("DBusPendingCallNotifyFunction")] IntPtr function, void* user_data, [NativeTypeName("DBusFreeFunction")] IntPtr free_user_data);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_pending_call_cancel(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_pending_call_get_completed(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusMessage* dbus_pending_call_steal_reply(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_pending_call_block(DBusPendingCall* pending);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_pending_call_allocate_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_pending_call_free_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_pending_call_set_data(DBusPendingCall* pending, [NativeTypeName("dbus_int32_t")] int slot, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_func);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_pending_call_get_data(DBusPendingCall* pending, [NativeTypeName("dbus_int32_t")] int slot);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusServer* dbus_server_listen([NativeTypeName("const char *")] byte* address, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial DBusServer* dbus_server_ref(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_server_unref(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_server_disconnect(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_get_is_connected(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_server_get_address(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_server_get_id(DBusServer* server);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_server_set_new_connection_function(DBusServer* server, [NativeTypeName("DBusNewConnectionFunction")] IntPtr function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_set_watch_functions(DBusServer* server, [NativeTypeName("DBusAddWatchFunction")] IntPtr add_function, [NativeTypeName("DBusRemoveWatchFunction")] IntPtr remove_function, [NativeTypeName("DBusWatchToggledFunction")] IntPtr toggled_function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_set_timeout_functions(DBusServer* server, [NativeTypeName("DBusAddTimeoutFunction")] IntPtr add_function, [NativeTypeName("DBusRemoveTimeoutFunction")] IntPtr remove_function, [NativeTypeName("DBusTimeoutToggledFunction")] IntPtr toggled_function, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_function);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_set_auth_mechanisms(DBusServer* server, [NativeTypeName("const char **")] byte** mechanisms);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_allocate_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_server_free_data_slot([NativeTypeName("dbus_int32_t *")] int* slot_p);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_server_set_data(DBusServer* server, int slot, void* data, [NativeTypeName("DBusFreeFunction")] IntPtr free_data_func);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void* dbus_server_get_data(DBusServer* server, int slot);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_signature_iter_init(DBusSignatureIter* iter, [NativeTypeName("const char *")] byte* signature);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_signature_iter_get_current_type([NativeTypeName("const DBusSignatureIter *")] DBusSignatureIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* dbus_signature_iter_get_signature([NativeTypeName("const DBusSignatureIter *")] DBusSignatureIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int dbus_signature_iter_get_element_type([NativeTypeName("const DBusSignatureIter *")] DBusSignatureIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_signature_iter_next(DBusSignatureIter* iter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void dbus_signature_iter_recurse([NativeTypeName("const DBusSignatureIter *")] DBusSignatureIter* iter, DBusSignatureIter* subiter);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_signature_validate([NativeTypeName("const char *")] byte* signature, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_signature_validate_single([NativeTypeName("const char *")] byte* signature, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_type_is_valid(int typecode);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_type_is_basic(int typecode);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_type_is_container(int typecode);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_type_is_fixed(int typecode);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_path([NativeTypeName("const char *")] byte* path, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_interface([NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_member([NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_error_name([NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_bus_name([NativeTypeName("const char *")] byte* name, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_validate_utf8([NativeTypeName("const char *")] byte* alleged_utf8, DBusError* error);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_threads_init([NativeTypeName("const DBusThreadFunctions *")] DBusThreadFunctions* functions);

        [LibraryImport("libdbus-1.so.3")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("dbus_bool_t")]
        public static partial uint dbus_threads_init_default();

        [NativeTypeName("#define TRUE 1")]
        public const int TRUE = 1;

        [NativeTypeName("#define FALSE 0")]
        public const int FALSE = 0;

        // [NativeTypeName("#define NULL ((void*) 0)")]
        // public static readonly void* NULL = null;

        [NativeTypeName("#define DBUS_LITTLE_ENDIAN ('l')")]
        public const int DBUS_LITTLE_ENDIAN = ('l');

        [NativeTypeName("#define DBUS_BIG_ENDIAN ('B')")]
        public const int DBUS_BIG_ENDIAN = ('B');

        [NativeTypeName("#define DBUS_MAJOR_PROTOCOL_VERSION 1")]
        public const int DBUS_MAJOR_PROTOCOL_VERSION = 1;

        [NativeTypeName("#define DBUS_TYPE_INVALID ((int) '\0')")]
        public const int DBUS_TYPE_INVALID = '\0';

        [NativeTypeName("#define DBUS_TYPE_INVALID_AS_STRING \"\0\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_INVALID_AS_STRING => new byte[] { 0x00, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_BYTE ((int) 'y')")]
        public const int DBUS_TYPE_BYTE = 'y';

        [NativeTypeName("#define DBUS_TYPE_BYTE_AS_STRING \"y\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_BYTE_AS_STRING => new byte[] { 0x79, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_BOOLEAN ((int) 'b')")]
        public const int DBUS_TYPE_BOOLEAN = 'b';

        [NativeTypeName("#define DBUS_TYPE_BOOLEAN_AS_STRING \"b\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_BOOLEAN_AS_STRING => new byte[] { 0x62, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_INT16 ((int) 'n')")]
        public const int DBUS_TYPE_INT16 = 'n';

        [NativeTypeName("#define DBUS_TYPE_INT16_AS_STRING \"n\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_INT16_AS_STRING => new byte[] { 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_UINT16 ((int) 'q')")]
        public const int DBUS_TYPE_UINT16 = 'q';

        [NativeTypeName("#define DBUS_TYPE_UINT16_AS_STRING \"q\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_UINT16_AS_STRING => new byte[] { 0x71, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_INT32 ((int) 'i')")]
        public const int DBUS_TYPE_INT32 = 'i';

        [NativeTypeName("#define DBUS_TYPE_INT32_AS_STRING \"i\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_INT32_AS_STRING => new byte[] { 0x69, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_UINT32 ((int) 'u')")]
        public const int DBUS_TYPE_UINT32 = 'u';

        [NativeTypeName("#define DBUS_TYPE_UINT32_AS_STRING \"u\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_UINT32_AS_STRING => new byte[] { 0x75, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_INT64 ((int) 'x')")]
        public const int DBUS_TYPE_INT64 = 'x';

        [NativeTypeName("#define DBUS_TYPE_INT64_AS_STRING \"x\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_INT64_AS_STRING => new byte[] { 0x78, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_UINT64 ((int) 't')")]
        public const int DBUS_TYPE_UINT64 = 't';

        [NativeTypeName("#define DBUS_TYPE_UINT64_AS_STRING \"t\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_UINT64_AS_STRING => new byte[] { 0x74, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_DOUBLE ((int) 'd')")]
        public const int DBUS_TYPE_DOUBLE = 'd';

        [NativeTypeName("#define DBUS_TYPE_DOUBLE_AS_STRING \"d\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_DOUBLE_AS_STRING => new byte[] { 0x64, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_STRING ((int) 's')")]
        public const int DBUS_TYPE_STRING = 's';

        [NativeTypeName("#define DBUS_TYPE_STRING_AS_STRING \"s\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_STRING_AS_STRING => new byte[] { 0x73, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_OBJECT_PATH ((int) 'o')")]
        public const int DBUS_TYPE_OBJECT_PATH = 'o';

        [NativeTypeName("#define DBUS_TYPE_OBJECT_PATH_AS_STRING \"o\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_OBJECT_PATH_AS_STRING => new byte[] { 0x6F, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_SIGNATURE ((int) 'g')")]
        public const int DBUS_TYPE_SIGNATURE = 'g';

        [NativeTypeName("#define DBUS_TYPE_SIGNATURE_AS_STRING \"g\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_SIGNATURE_AS_STRING => new byte[] { 0x67, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_UNIX_FD ((int) 'h')")]
        public const int DBUS_TYPE_UNIX_FD = 'h';

        [NativeTypeName("#define DBUS_TYPE_UNIX_FD_AS_STRING \"h\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_UNIX_FD_AS_STRING => new byte[] { 0x68, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_ARRAY ((int) 'a')")]
        public const int DBUS_TYPE_ARRAY = 'a';

        [NativeTypeName("#define DBUS_TYPE_ARRAY_AS_STRING \"a\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_ARRAY_AS_STRING => new byte[] { 0x61, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_VARIANT ((int) 'v')")]
        public const int DBUS_TYPE_VARIANT = 'v';

        [NativeTypeName("#define DBUS_TYPE_VARIANT_AS_STRING \"v\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_VARIANT_AS_STRING => new byte[] { 0x76, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_STRUCT ((int) 'r')")]
        public const int DBUS_TYPE_STRUCT = 'r';

        [NativeTypeName("#define DBUS_TYPE_STRUCT_AS_STRING \"r\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_STRUCT_AS_STRING => new byte[] { 0x72, 0x00 };

        [NativeTypeName("#define DBUS_TYPE_DICT_ENTRY ((int) 'e')")]
        public const int DBUS_TYPE_DICT_ENTRY = 'e';

        [NativeTypeName("#define DBUS_TYPE_DICT_ENTRY_AS_STRING \"e\"")]
        public static ReadOnlySpan<byte> DBUS_TYPE_DICT_ENTRY_AS_STRING => new byte[] { 0x65, 0x00 };

        [NativeTypeName("#define DBUS_NUMBER_OF_TYPES (16)")]
        public const int DBUS_NUMBER_OF_TYPES = (16);

        [NativeTypeName("#define DBUS_STRUCT_BEGIN_CHAR ((int) '(')")]
        public const int DBUS_STRUCT_BEGIN_CHAR = '(';

        [NativeTypeName("#define DBUS_STRUCT_BEGIN_CHAR_AS_STRING \"(\"")]
        public static ReadOnlySpan<byte> DBUS_STRUCT_BEGIN_CHAR_AS_STRING => new byte[] { 0x28, 0x00 };

        [NativeTypeName("#define DBUS_STRUCT_END_CHAR ((int) ')')")]
        public const int DBUS_STRUCT_END_CHAR = ')';

        [NativeTypeName("#define DBUS_STRUCT_END_CHAR_AS_STRING \")\"")]
        public static ReadOnlySpan<byte> DBUS_STRUCT_END_CHAR_AS_STRING => new byte[] { 0x29, 0x00 };

        [NativeTypeName("#define DBUS_DICT_ENTRY_BEGIN_CHAR ((int) '{')")]
        public const int DBUS_DICT_ENTRY_BEGIN_CHAR = '{';

        [NativeTypeName("#define DBUS_DICT_ENTRY_BEGIN_CHAR_AS_STRING \"{\"")]
        public static ReadOnlySpan<byte> DBUS_DICT_ENTRY_BEGIN_CHAR_AS_STRING => new byte[] { 0x7B, 0x00 };

        [NativeTypeName("#define DBUS_DICT_ENTRY_END_CHAR ((int) '}')")]
        public const int DBUS_DICT_ENTRY_END_CHAR = '}';

        [NativeTypeName("#define DBUS_DICT_ENTRY_END_CHAR_AS_STRING \"}\"")]
        public static ReadOnlySpan<byte> DBUS_DICT_ENTRY_END_CHAR_AS_STRING => new byte[] { 0x7D, 0x00 };

        [NativeTypeName("#define DBUS_MAXIMUM_NAME_LENGTH 255")]
        public const int DBUS_MAXIMUM_NAME_LENGTH = 255;

        [NativeTypeName("#define DBUS_MAXIMUM_SIGNATURE_LENGTH 255")]
        public const int DBUS_MAXIMUM_SIGNATURE_LENGTH = 255;

        [NativeTypeName("#define DBUS_MAXIMUM_MATCH_RULE_LENGTH 1024")]
        public const int DBUS_MAXIMUM_MATCH_RULE_LENGTH = 1024;

        [NativeTypeName("#define DBUS_MAXIMUM_MATCH_RULE_ARG_NUMBER 63")]
        public const int DBUS_MAXIMUM_MATCH_RULE_ARG_NUMBER = 63;

        [NativeTypeName("#define DBUS_MAXIMUM_ARRAY_LENGTH (67108864)")]
        public const int DBUS_MAXIMUM_ARRAY_LENGTH = (67108864);

        [NativeTypeName("#define DBUS_MAXIMUM_ARRAY_LENGTH_BITS 26")]
        public const int DBUS_MAXIMUM_ARRAY_LENGTH_BITS = 26;

        [NativeTypeName("#define DBUS_MAXIMUM_MESSAGE_LENGTH (DBUS_MAXIMUM_ARRAY_LENGTH * 2)")]
        public const int DBUS_MAXIMUM_MESSAGE_LENGTH = ((67108864) * 2);

        [NativeTypeName("#define DBUS_MAXIMUM_MESSAGE_LENGTH_BITS 27")]
        public const int DBUS_MAXIMUM_MESSAGE_LENGTH_BITS = 27;

        [NativeTypeName("#define DBUS_MAXIMUM_MESSAGE_UNIX_FDS (DBUS_MAXIMUM_MESSAGE_LENGTH/4)")]
        public const int DBUS_MAXIMUM_MESSAGE_UNIX_FDS = (((67108864) * 2) / 4);

        [NativeTypeName("#define DBUS_MAXIMUM_MESSAGE_UNIX_FDS_BITS (DBUS_MAXIMUM_MESSAGE_LENGTH_BITS-2)")]
        public const int DBUS_MAXIMUM_MESSAGE_UNIX_FDS_BITS = (27 - 2);

        [NativeTypeName("#define DBUS_MAXIMUM_TYPE_RECURSION_DEPTH 32")]
        public const int DBUS_MAXIMUM_TYPE_RECURSION_DEPTH = 32;

        [NativeTypeName("#define DBUS_MESSAGE_TYPE_INVALID 0")]
        public const int DBUS_MESSAGE_TYPE_INVALID = 0;

        [NativeTypeName("#define DBUS_MESSAGE_TYPE_METHOD_CALL 1")]
        public const int DBUS_MESSAGE_TYPE_METHOD_CALL = 1;

        [NativeTypeName("#define DBUS_MESSAGE_TYPE_METHOD_RETURN 2")]
        public const int DBUS_MESSAGE_TYPE_METHOD_RETURN = 2;

        [NativeTypeName("#define DBUS_MESSAGE_TYPE_ERROR 3")]
        public const int DBUS_MESSAGE_TYPE_ERROR = 3;

        [NativeTypeName("#define DBUS_MESSAGE_TYPE_SIGNAL 4")]
        public const int DBUS_MESSAGE_TYPE_SIGNAL = 4;

        [NativeTypeName("#define DBUS_NUM_MESSAGE_TYPES 5")]
        public const int DBUS_NUM_MESSAGE_TYPES = 5;

        [NativeTypeName("#define DBUS_HEADER_FLAG_NO_REPLY_EXPECTED 0x1")]
        public const int DBUS_HEADER_FLAG_NO_REPLY_EXPECTED = 0x1;

        [NativeTypeName("#define DBUS_HEADER_FLAG_NO_AUTO_START 0x2")]
        public const int DBUS_HEADER_FLAG_NO_AUTO_START = 0x2;

        [NativeTypeName("#define DBUS_HEADER_FLAG_ALLOW_INTERACTIVE_AUTHORIZATION 0x4")]
        public const int DBUS_HEADER_FLAG_ALLOW_INTERACTIVE_AUTHORIZATION = 0x4;

        [NativeTypeName("#define DBUS_HEADER_FIELD_INVALID 0")]
        public const int DBUS_HEADER_FIELD_INVALID = 0;

        [NativeTypeName("#define DBUS_HEADER_FIELD_PATH 1")]
        public const int DBUS_HEADER_FIELD_PATH = 1;

        [NativeTypeName("#define DBUS_HEADER_FIELD_INTERFACE 2")]
        public const int DBUS_HEADER_FIELD_INTERFACE = 2;

        [NativeTypeName("#define DBUS_HEADER_FIELD_MEMBER 3")]
        public const int DBUS_HEADER_FIELD_MEMBER = 3;

        [NativeTypeName("#define DBUS_HEADER_FIELD_ERROR_NAME 4")]
        public const int DBUS_HEADER_FIELD_ERROR_NAME = 4;

        [NativeTypeName("#define DBUS_HEADER_FIELD_REPLY_SERIAL 5")]
        public const int DBUS_HEADER_FIELD_REPLY_SERIAL = 5;

        [NativeTypeName("#define DBUS_HEADER_FIELD_DESTINATION 6")]
        public const int DBUS_HEADER_FIELD_DESTINATION = 6;

        [NativeTypeName("#define DBUS_HEADER_FIELD_SENDER 7")]
        public const int DBUS_HEADER_FIELD_SENDER = 7;

        [NativeTypeName("#define DBUS_HEADER_FIELD_SIGNATURE 8")]
        public const int DBUS_HEADER_FIELD_SIGNATURE = 8;

        [NativeTypeName("#define DBUS_HEADER_FIELD_UNIX_FDS 9")]
        public const int DBUS_HEADER_FIELD_UNIX_FDS = 9;

        [NativeTypeName("#define DBUS_HEADER_FIELD_CONTAINER_INSTANCE 10")]
        public const int DBUS_HEADER_FIELD_CONTAINER_INSTANCE = 10;

        [NativeTypeName("#define DBUS_HEADER_FIELD_LAST DBUS_HEADER_FIELD_CONTAINER_INSTANCE")]
        public const int DBUS_HEADER_FIELD_LAST = 10;

        [NativeTypeName("#define DBUS_HEADER_SIGNATURE DBUS_TYPE_BYTE_AS_STRING                   \\\n     DBUS_TYPE_BYTE_AS_STRING                   \\\n     DBUS_TYPE_BYTE_AS_STRING                   \\\n     DBUS_TYPE_BYTE_AS_STRING                   \\\n     DBUS_TYPE_UINT32_AS_STRING                 \\\n     DBUS_TYPE_UINT32_AS_STRING                 \\\n     DBUS_TYPE_ARRAY_AS_STRING                  \\\n     DBUS_STRUCT_BEGIN_CHAR_AS_STRING           \\\n     DBUS_TYPE_BYTE_AS_STRING                   \\\n     DBUS_TYPE_VARIANT_AS_STRING                \\\n     DBUS_STRUCT_END_CHAR_AS_STRING")]
        public static ReadOnlySpan<byte> DBUS_HEADER_SIGNATURE => new byte[] { 0x79, 0x79, 0x79, 0x79, 0x75, 0x75, 0x61, 0x28, 0x79, 0x76, 0x29, 0x00 };

        [NativeTypeName("#define DBUS_MINIMUM_HEADER_SIZE 16")]
        public const int DBUS_MINIMUM_HEADER_SIZE = 16;

        [NativeTypeName("#define DBUS_ERROR_FAILED \"org.freedesktop.DBus.Error.Failed\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NO_MEMORY \"org.freedesktop.DBus.Error.NoMemory\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NO_MEMORY => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x4D, 0x65, 0x6D, 0x6F, 0x72, 0x79, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SERVICE_UNKNOWN \"org.freedesktop.DBus.Error.ServiceUnknown\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SERVICE_UNKNOWN => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NAME_HAS_NO_OWNER \"org.freedesktop.DBus.Error.NameHasNoOwner\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NAME_HAS_NO_OWNER => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x61, 0x6D, 0x65, 0x48, 0x61, 0x73, 0x4E, 0x6F, 0x4F, 0x77, 0x6E, 0x65, 0x72, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NO_REPLY \"org.freedesktop.DBus.Error.NoReply\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NO_REPLY => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x52, 0x65, 0x70, 0x6C, 0x79, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_IO_ERROR \"org.freedesktop.DBus.Error.IOError\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_IO_ERROR => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x4F, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_BAD_ADDRESS \"org.freedesktop.DBus.Error.BadAddress\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_BAD_ADDRESS => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x42, 0x61, 0x64, 0x41, 0x64, 0x64, 0x72, 0x65, 0x73, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NOT_SUPPORTED \"org.freedesktop.DBus.Error.NotSupported\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NOT_SUPPORTED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x74, 0x53, 0x75, 0x70, 0x70, 0x6F, 0x72, 0x74, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_LIMITS_EXCEEDED \"org.freedesktop.DBus.Error.LimitsExceeded\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_LIMITS_EXCEEDED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4C, 0x69, 0x6D, 0x69, 0x74, 0x73, 0x45, 0x78, 0x63, 0x65, 0x65, 0x64, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_ACCESS_DENIED \"org.freedesktop.DBus.Error.AccessDenied\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_ACCESS_DENIED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x41, 0x63, 0x63, 0x65, 0x73, 0x73, 0x44, 0x65, 0x6E, 0x69, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_AUTH_FAILED \"org.freedesktop.DBus.Error.AuthFailed\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_AUTH_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x41, 0x75, 0x74, 0x68, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NO_SERVER \"org.freedesktop.DBus.Error.NoServer\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NO_SERVER => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_TIMEOUT \"org.freedesktop.DBus.Error.Timeout\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_TIMEOUT => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x54, 0x69, 0x6D, 0x65, 0x6F, 0x75, 0x74, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NO_NETWORK \"org.freedesktop.DBus.Error.NoNetwork\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NO_NETWORK => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x4E, 0x65, 0x74, 0x77, 0x6F, 0x72, 0x6B, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_ADDRESS_IN_USE \"org.freedesktop.DBus.Error.AddressInUse\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_ADDRESS_IN_USE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x41, 0x64, 0x64, 0x72, 0x65, 0x73, 0x73, 0x49, 0x6E, 0x55, 0x73, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_DISCONNECTED \"org.freedesktop.DBus.Error.Disconnected\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_DISCONNECTED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x44, 0x69, 0x73, 0x63, 0x6F, 0x6E, 0x6E, 0x65, 0x63, 0x74, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_INVALID_ARGS \"org.freedesktop.DBus.Error.InvalidArgs\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_INVALID_ARGS => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x41, 0x72, 0x67, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_FILE_NOT_FOUND \"org.freedesktop.DBus.Error.FileNotFound\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_FILE_NOT_FOUND => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x46, 0x69, 0x6C, 0x65, 0x4E, 0x6F, 0x74, 0x46, 0x6F, 0x75, 0x6E, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_FILE_EXISTS \"org.freedesktop.DBus.Error.FileExists\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_FILE_EXISTS => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x46, 0x69, 0x6C, 0x65, 0x45, 0x78, 0x69, 0x73, 0x74, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_UNKNOWN_METHOD \"org.freedesktop.DBus.Error.UnknownMethod\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_UNKNOWN_METHOD => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x4D, 0x65, 0x74, 0x68, 0x6F, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_UNKNOWN_OBJECT \"org.freedesktop.DBus.Error.UnknownObject\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_UNKNOWN_OBJECT => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x4F, 0x62, 0x6A, 0x65, 0x63, 0x74, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_UNKNOWN_INTERFACE \"org.freedesktop.DBus.Error.UnknownInterface\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_UNKNOWN_INTERFACE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x49, 0x6E, 0x74, 0x65, 0x72, 0x66, 0x61, 0x63, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_UNKNOWN_PROPERTY \"org.freedesktop.DBus.Error.UnknownProperty\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_UNKNOWN_PROPERTY => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_PROPERTY_READ_ONLY \"org.freedesktop.DBus.Error.PropertyReadOnly\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_PROPERTY_READ_ONLY => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x52, 0x65, 0x61, 0x64, 0x4F, 0x6E, 0x6C, 0x79, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_TIMED_OUT \"org.freedesktop.DBus.Error.TimedOut\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_TIMED_OUT => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x54, 0x69, 0x6D, 0x65, 0x64, 0x4F, 0x75, 0x74, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_MATCH_RULE_NOT_FOUND \"org.freedesktop.DBus.Error.MatchRuleNotFound\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_MATCH_RULE_NOT_FOUND => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4D, 0x61, 0x74, 0x63, 0x68, 0x52, 0x75, 0x6C, 0x65, 0x4E, 0x6F, 0x74, 0x46, 0x6F, 0x75, 0x6E, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_MATCH_RULE_INVALID \"org.freedesktop.DBus.Error.MatchRuleInvalid\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_MATCH_RULE_INVALID => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4D, 0x61, 0x74, 0x63, 0x68, 0x52, 0x75, 0x6C, 0x65, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_EXEC_FAILED \"org.freedesktop.DBus.Error.Spawn.ExecFailed\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_EXEC_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x45, 0x78, 0x65, 0x63, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_FORK_FAILED \"org.freedesktop.DBus.Error.Spawn.ForkFailed\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_FORK_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x46, 0x6F, 0x72, 0x6B, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_CHILD_EXITED \"org.freedesktop.DBus.Error.Spawn.ChildExited\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_CHILD_EXITED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x43, 0x68, 0x69, 0x6C, 0x64, 0x45, 0x78, 0x69, 0x74, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_CHILD_SIGNALED \"org.freedesktop.DBus.Error.Spawn.ChildSignaled\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_CHILD_SIGNALED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x43, 0x68, 0x69, 0x6C, 0x64, 0x53, 0x69, 0x67, 0x6E, 0x61, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_FAILED \"org.freedesktop.DBus.Error.Spawn.Failed\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_SETUP_FAILED \"org.freedesktop.DBus.Error.Spawn.FailedToSetup\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_SETUP_FAILED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x46, 0x61, 0x69, 0x6C, 0x65, 0x64, 0x54, 0x6F, 0x53, 0x65, 0x74, 0x75, 0x70, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_CONFIG_INVALID \"org.freedesktop.DBus.Error.Spawn.ConfigInvalid\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_CONFIG_INVALID => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x43, 0x6F, 0x6E, 0x66, 0x69, 0x67, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_SERVICE_INVALID \"org.freedesktop.DBus.Error.Spawn.ServiceNotValid\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_SERVICE_INVALID => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x53, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0x4E, 0x6F, 0x74, 0x56, 0x61, 0x6C, 0x69, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_SERVICE_NOT_FOUND \"org.freedesktop.DBus.Error.Spawn.ServiceNotFound\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_SERVICE_NOT_FOUND => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x53, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0x4E, 0x6F, 0x74, 0x46, 0x6F, 0x75, 0x6E, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_PERMISSIONS_INVALID \"org.freedesktop.DBus.Error.Spawn.PermissionsInvalid\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_PERMISSIONS_INVALID => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x50, 0x65, 0x72, 0x6D, 0x69, 0x73, 0x73, 0x69, 0x6F, 0x6E, 0x73, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_FILE_INVALID \"org.freedesktop.DBus.Error.Spawn.FileInvalid\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_FILE_INVALID => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x46, 0x69, 0x6C, 0x65, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SPAWN_NO_MEMORY \"org.freedesktop.DBus.Error.Spawn.NoMemory\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SPAWN_NO_MEMORY => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x70, 0x61, 0x77, 0x6E, 0x2E, 0x4E, 0x6F, 0x4D, 0x65, 0x6D, 0x6F, 0x72, 0x79, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_UNIX_PROCESS_ID_UNKNOWN \"org.freedesktop.DBus.Error.UnixProcessIdUnknown\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_UNIX_PROCESS_ID_UNKNOWN => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x55, 0x6E, 0x69, 0x78, 0x50, 0x72, 0x6F, 0x63, 0x65, 0x73, 0x73, 0x49, 0x64, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_INVALID_SIGNATURE \"org.freedesktop.DBus.Error.InvalidSignature\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_INVALID_SIGNATURE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x53, 0x69, 0x67, 0x6E, 0x61, 0x74, 0x75, 0x72, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_INVALID_FILE_CONTENT \"org.freedesktop.DBus.Error.InvalidFileContent\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_INVALID_FILE_CONTENT => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x6E, 0x76, 0x61, 0x6C, 0x69, 0x64, 0x46, 0x69, 0x6C, 0x65, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x6E, 0x74, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_SELINUX_SECURITY_CONTEXT_UNKNOWN \"org.freedesktop.DBus.Error.SELinuxSecurityContextUnknown\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_SELINUX_SECURITY_CONTEXT_UNKNOWN => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x53, 0x45, 0x4C, 0x69, 0x6E, 0x75, 0x78, 0x53, 0x65, 0x63, 0x75, 0x72, 0x69, 0x74, 0x79, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x78, 0x74, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_APPARMOR_SECURITY_CONTEXT_UNKNOWN \"org.freedesktop.DBus.Error.AppArmorSecurityContextUnknown\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_APPARMOR_SECURITY_CONTEXT_UNKNOWN => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x41, 0x70, 0x70, 0x41, 0x72, 0x6D, 0x6F, 0x72, 0x53, 0x65, 0x63, 0x75, 0x72, 0x69, 0x74, 0x79, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x78, 0x74, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_ADT_AUDIT_DATA_UNKNOWN \"org.freedesktop.DBus.Error.AdtAuditDataUnknown\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_ADT_AUDIT_DATA_UNKNOWN => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x41, 0x64, 0x74, 0x41, 0x75, 0x64, 0x69, 0x74, 0x44, 0x61, 0x74, 0x61, 0x55, 0x6E, 0x6B, 0x6E, 0x6F, 0x77, 0x6E, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_OBJECT_PATH_IN_USE \"org.freedesktop.DBus.Error.ObjectPathInUse\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_OBJECT_PATH_IN_USE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4F, 0x62, 0x6A, 0x65, 0x63, 0x74, 0x50, 0x61, 0x74, 0x68, 0x49, 0x6E, 0x55, 0x73, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_INCONSISTENT_MESSAGE \"org.freedesktop.DBus.Error.InconsistentMessage\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_INCONSISTENT_MESSAGE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x6E, 0x63, 0x6F, 0x6E, 0x73, 0x69, 0x73, 0x74, 0x65, 0x6E, 0x74, 0x4D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_INTERACTIVE_AUTHORIZATION_REQUIRED \"org.freedesktop.DBus.Error.InteractiveAuthorizationRequired\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_INTERACTIVE_AUTHORIZATION_REQUIRED => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x49, 0x6E, 0x74, 0x65, 0x72, 0x61, 0x63, 0x74, 0x69, 0x76, 0x65, 0x41, 0x75, 0x74, 0x68, 0x6F, 0x72, 0x69, 0x7A, 0x61, 0x74, 0x69, 0x6F, 0x6E, 0x52, 0x65, 0x71, 0x75, 0x69, 0x72, 0x65, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_ERROR_NOT_CONTAINER \"org.freedesktop.DBus.Error.NotContainer\"")]
        public static ReadOnlySpan<byte> DBUS_ERROR_NOT_CONTAINER => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x45, 0x72, 0x72, 0x6F, 0x72, 0x2E, 0x4E, 0x6F, 0x74, 0x43, 0x6F, 0x6E, 0x74, 0x61, 0x69, 0x6E, 0x65, 0x72, 0x00 };

        [NativeTypeName("#define DBUS_INTROSPECT_1_0_XML_NAMESPACE \"http://www.freedesktop.org/standards/dbus\"")]
        public static ReadOnlySpan<byte> DBUS_INTROSPECT_1_0_XML_NAMESPACE => new byte[] { 0x68, 0x74, 0x74, 0x70, 0x3A, 0x2F, 0x2F, 0x77, 0x77, 0x77, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x6F, 0x72, 0x67, 0x2F, 0x73, 0x74, 0x61, 0x6E, 0x64, 0x61, 0x72, 0x64, 0x73, 0x2F, 0x64, 0x62, 0x75, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_INTROSPECT_1_0_XML_PUBLIC_IDENTIFIER \"-//freedesktop//DTD D-BUS Object Introspection 1.0//EN\"")]
        public static ReadOnlySpan<byte> DBUS_INTROSPECT_1_0_XML_PUBLIC_IDENTIFIER => new byte[] { 0x2D, 0x2F, 0x2F, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2F, 0x2F, 0x44, 0x54, 0x44, 0x20, 0x44, 0x2D, 0x42, 0x55, 0x53, 0x20, 0x4F, 0x62, 0x6A, 0x65, 0x63, 0x74, 0x20, 0x49, 0x6E, 0x74, 0x72, 0x6F, 0x73, 0x70, 0x65, 0x63, 0x74, 0x69, 0x6F, 0x6E, 0x20, 0x31, 0x2E, 0x30, 0x2F, 0x2F, 0x45, 0x4E, 0x00 };

        [NativeTypeName("#define DBUS_INTROSPECT_1_0_XML_SYSTEM_IDENTIFIER \"http://www.freedesktop.org/standards/dbus/1.0/introspect.dtd\"")]
        public static ReadOnlySpan<byte> DBUS_INTROSPECT_1_0_XML_SYSTEM_IDENTIFIER => new byte[] { 0x68, 0x74, 0x74, 0x70, 0x3A, 0x2F, 0x2F, 0x77, 0x77, 0x77, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x6F, 0x72, 0x67, 0x2F, 0x73, 0x74, 0x61, 0x6E, 0x64, 0x61, 0x72, 0x64, 0x73, 0x2F, 0x64, 0x62, 0x75, 0x73, 0x2F, 0x31, 0x2E, 0x30, 0x2F, 0x69, 0x6E, 0x74, 0x72, 0x6F, 0x73, 0x70, 0x65, 0x63, 0x74, 0x2E, 0x64, 0x74, 0x64, 0x00 };

        [NativeTypeName("#define DBUS_INTROSPECT_1_0_XML_DOCTYPE_DECL_NODE \"<!DOCTYPE node internal \"\" DBUS_INTROSPECT_1_0_XML_PUBLIC_IDENTIFIER \"\"\n\"\" DBUS_INTROSPECT_1_0_XML_SYSTEM_IDENTIFIER \"\">\n\"")]
        public static ReadOnlySpan<byte> DBUS_INTROSPECT_1_0_XML_DOCTYPE_DECL_NODE => new byte[] { 0x3C, 0x21, 0x44, 0x4F, 0x43, 0x54, 0x59, 0x50, 0x45, 0x20, 0x6E, 0x6F, 0x64, 0x65, 0x20, 0x50, 0x55, 0x42, 0x4C, 0x49, 0x43, 0x20, 0x22, 0x2D, 0x2F, 0x2F, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2F, 0x2F, 0x44, 0x54, 0x44, 0x20, 0x44, 0x2D, 0x42, 0x55, 0x53, 0x20, 0x4F, 0x62, 0x6A, 0x65, 0x63, 0x74, 0x20, 0x49, 0x6E, 0x74, 0x72, 0x6F, 0x73, 0x70, 0x65, 0x63, 0x74, 0x69, 0x6F, 0x6E, 0x20, 0x31, 0x2E, 0x30, 0x2F, 0x2F, 0x45, 0x4E, 0x22, 0x0A, 0x22, 0x68, 0x74, 0x74, 0x70, 0x3A, 0x2F, 0x2F, 0x77, 0x77, 0x77, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x6F, 0x72, 0x67, 0x2F, 0x73, 0x74, 0x61, 0x6E, 0x64, 0x61, 0x72, 0x64, 0x73, 0x2F, 0x64, 0x62, 0x75, 0x73, 0x2F, 0x31, 0x2E, 0x30, 0x2F, 0x69, 0x6E, 0x74, 0x72, 0x6F, 0x73, 0x70, 0x65, 0x63, 0x74, 0x2E, 0x64, 0x74, 0x64, 0x22, 0x3E, 0x0A, 0x00 };

        [NativeTypeName("#define DBUS_SERVICE_DBUS \"org.freedesktop.DBus\"")]
        public static ReadOnlySpan<byte> DBUS_SERVICE_DBUS => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_PATH_DBUS \"/org/freedesktop/DBus\"")]
        public static ReadOnlySpan<byte> DBUS_PATH_DBUS => new byte[] { 0x2F, 0x6F, 0x72, 0x67, 0x2F, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2F, 0x44, 0x42, 0x75, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_PATH_LOCAL \"/org/freedesktop/DBus/Local\"")]
        public static ReadOnlySpan<byte> DBUS_PATH_LOCAL => new byte[] { 0x2F, 0x6F, 0x72, 0x67, 0x2F, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2F, 0x44, 0x42, 0x75, 0x73, 0x2F, 0x4C, 0x6F, 0x63, 0x61, 0x6C, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_DBUS \"org.freedesktop.DBus\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_DBUS => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_MONITORING \"org.freedesktop.DBus.Monitoring\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_MONITORING => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x4D, 0x6F, 0x6E, 0x69, 0x74, 0x6F, 0x72, 0x69, 0x6E, 0x67, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_VERBOSE \"org.freedesktop.DBus.Verbose\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_VERBOSE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x56, 0x65, 0x72, 0x62, 0x6F, 0x73, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_INTROSPECTABLE \"org.freedesktop.DBus.Introspectable\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_INTROSPECTABLE => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x49, 0x6E, 0x74, 0x72, 0x6F, 0x73, 0x70, 0x65, 0x63, 0x74, 0x61, 0x62, 0x6C, 0x65, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_PROPERTIES \"org.freedesktop.DBus.Properties\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_PROPERTIES => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x69, 0x65, 0x73, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_PEER \"org.freedesktop.DBus.Peer\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_PEER => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x50, 0x65, 0x65, 0x72, 0x00 };

        [NativeTypeName("#define DBUS_INTERFACE_LOCAL \"org.freedesktop.DBus.Local\"")]
        public static ReadOnlySpan<byte> DBUS_INTERFACE_LOCAL => new byte[] { 0x6F, 0x72, 0x67, 0x2E, 0x66, 0x72, 0x65, 0x65, 0x64, 0x65, 0x73, 0x6B, 0x74, 0x6F, 0x70, 0x2E, 0x44, 0x42, 0x75, 0x73, 0x2E, 0x4C, 0x6F, 0x63, 0x61, 0x6C, 0x00 };

        [NativeTypeName("#define DBUS_NAME_FLAG_ALLOW_REPLACEMENT 0x1")]
        public const int DBUS_NAME_FLAG_ALLOW_REPLACEMENT = 0x1;

        [NativeTypeName("#define DBUS_NAME_FLAG_REPLACE_EXISTING 0x2")]
        public const int DBUS_NAME_FLAG_REPLACE_EXISTING = 0x2;

        [NativeTypeName("#define DBUS_NAME_FLAG_DO_NOT_QUEUE 0x4")]
        public const int DBUS_NAME_FLAG_DO_NOT_QUEUE = 0x4;

        [NativeTypeName("#define DBUS_REQUEST_NAME_REPLY_PRIMARY_OWNER 1")]
        public const int DBUS_REQUEST_NAME_REPLY_PRIMARY_OWNER = 1;

        [NativeTypeName("#define DBUS_REQUEST_NAME_REPLY_IN_QUEUE 2")]
        public const int DBUS_REQUEST_NAME_REPLY_IN_QUEUE = 2;

        [NativeTypeName("#define DBUS_REQUEST_NAME_REPLY_EXISTS 3")]
        public const int DBUS_REQUEST_NAME_REPLY_EXISTS = 3;

        [NativeTypeName("#define DBUS_REQUEST_NAME_REPLY_ALREADY_OWNER 4")]
        public const int DBUS_REQUEST_NAME_REPLY_ALREADY_OWNER = 4;

        [NativeTypeName("#define DBUS_RELEASE_NAME_REPLY_RELEASED 1")]
        public const int DBUS_RELEASE_NAME_REPLY_RELEASED = 1;

        [NativeTypeName("#define DBUS_RELEASE_NAME_REPLY_NON_EXISTENT 2")]
        public const int DBUS_RELEASE_NAME_REPLY_NON_EXISTENT = 2;

        [NativeTypeName("#define DBUS_RELEASE_NAME_REPLY_NOT_OWNER 3")]
        public const int DBUS_RELEASE_NAME_REPLY_NOT_OWNER = 3;

        [NativeTypeName("#define DBUS_START_REPLY_SUCCESS 1")]
        public const int DBUS_START_REPLY_SUCCESS = 1;

        [NativeTypeName("#define DBUS_START_REPLY_ALREADY_RUNNING 2")]
        public const int DBUS_START_REPLY_ALREADY_RUNNING = 2;

        [NativeTypeName("#define DBUS_TIMEOUT_INFINITE ((int) 0x7fffffff)")]
        public const int DBUS_TIMEOUT_INFINITE = 0x7fffffff;

        [NativeTypeName("#define DBUS_TIMEOUT_USE_DEFAULT (-1)")]
        public const int DBUS_TIMEOUT_USE_DEFAULT = (-1);
    }
}

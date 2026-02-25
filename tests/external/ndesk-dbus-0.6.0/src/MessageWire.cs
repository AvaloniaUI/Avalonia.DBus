// Copyright 2006 Alp Toker <alp@atoker.com>
// Copyright 2026 AvaloniaUI OÜ

// This software is made available under the MIT License
// See COPYING for details

using System;

namespace NDesk.DBus
{
	static class MessageWire
	{
		public static byte[] Marshal (Message msg)
		{
			if (msg == null)
				throw new ArgumentNullException ("msg");

			byte[] header = msg.GetHeaderData ();
			byte[] body = msg.Body ?? Array.Empty<byte> ();

			byte[] data = new byte[header.Length + body.Length];
			Array.Copy (header, 0, data, 0, header.Length);
			Array.Copy (body, 0, data, header.Length, body.Length);
			return data;
		}

		public static int BytesNeeded (byte[] partial, int length)
		{
			if (partial == null || length < 16 || partial.Length < 16)
				return 0;

			EndianFlag endianness = (EndianFlag)partial[0];
			if (endianness != EndianFlag.Little && endianness != EndianFlag.Big)
				return -1;

			if (partial[3] != Protocol.Version)
				return -1;

			uint bodyLen;
			uint headerLen;

			if (endianness == EndianFlag.Little) {
				bodyLen = (uint)(partial[4] | (partial[5] << 8) | (partial[6] << 16) | (partial[7] << 24));
				headerLen = (uint)(partial[12] | (partial[13] << 8) | (partial[14] << 16) | (partial[15] << 24));
			} else {
				bodyLen = (uint)((partial[4] << 24) | (partial[5] << 16) | (partial[6] << 8) | partial[7]);
				headerLen = (uint)((partial[12] << 24) | (partial[13] << 16) | (partial[14] << 8) | partial[15]);
			}

			long total = 16L + Protocol.Padded ((int)headerLen, 8) + bodyLen;
			if (total > Protocol.MaxMessageLength || total > Int32.MaxValue)
				return -1;

			return (int)total;
		}

		public static Message Demarshal (byte[] data)
		{
			if (data == null || data.Length < 16)
				throw new ArgumentException ("Invalid message data: too short");

			int needed = BytesNeeded (data, data.Length);
			if (needed == -1)
				throw new ArgumentException ("Invalid message data");
			if (data.Length < needed)
				throw new ArgumentException ("Invalid message data: incomplete");

			EndianFlag endianness = (EndianFlag)data[0];

			uint bodyLen;
			uint headerLen;
			if (endianness == EndianFlag.Little) {
				bodyLen = (uint)(data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24));
				headerLen = (uint)(data[12] | (data[13] << 8) | (data[14] << 16) | (data[15] << 24));
			} else {
				bodyLen = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
				headerLen = (uint)((data[12] << 24) | (data[13] << 16) | (data[14] << 8) | data[15]);
			}

			int paddedHeaderLen = 16 + Protocol.Padded ((int)headerLen, 8);
			if (paddedHeaderLen < 16 || paddedHeaderLen > data.Length)
				throw new ArgumentException ("Invalid message data");

			byte[] header = new byte[paddedHeaderLen];
			Array.Copy (data, 0, header, 0, header.Length);

			byte[] body = null;
			if (bodyLen > 0) {
				if (paddedHeaderLen + bodyLen > data.Length)
					throw new ArgumentException ("Invalid message data");

				body = new byte[bodyLen];
				Array.Copy (data, paddedHeaderLen, body, 0, (int)bodyLen);
			}

			Message msg = new Message ();
			msg.Body = body;
			msg.SetHeaderData (header);

			return msg;
		}
	}
}

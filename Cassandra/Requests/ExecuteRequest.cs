﻿namespace Cassandra
{
    internal class ExecuteRequest : IRequest
    {
        public const byte OpCode = 0x0A;

        readonly int _streamId;
        readonly object[] _values;
        readonly byte[] _id;
        readonly RowSetMetadata _metadata;
        readonly ConsistencyLevel _consistency;
        private readonly byte _flags = 0x00;

        public ExecuteRequest(int streamId, byte[] id, RowSetMetadata metadata, object[] values, ConsistencyLevel consistency, bool tracingEnabled)
        {
            this._streamId = streamId;
            this._values = values;
            this._id = id;
            this._metadata = metadata;
            this._consistency = consistency;
            if (tracingEnabled)
                this._flags = 0x02;
        }

        public RequestFrame GetFrame()
        {
            var wb = new BEBinaryWriter();
            wb.WriteFrameHeader(0x01, _flags, (byte)_streamId, OpCode);
            wb.WriteShortBytes(_id);
            wb.WriteUInt16((ushort) _values.Length);
            for(int i =0;i<_metadata.Columns.Length;i++)
            {
                var bytes = _metadata.ConvertFromObject(i,_values[i]);
                wb.WriteBytes(bytes);
            }
            wb.WriteInt16((short)_consistency);
            return wb.GetFrame();
        }
    }
}

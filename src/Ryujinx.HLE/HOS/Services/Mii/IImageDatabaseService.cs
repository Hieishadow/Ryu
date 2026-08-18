using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Mii.Types;

namespace Ryujinx.HLE.HOS.Services.Mii
{
    [Service("miiimg")] // 5.0.0+
    class IImageDatabaseService : IpcService
    {
        private uint _imageCount;
        private bool _isDirty;
        private readonly DatabaseSessionMetadata _metadata;

        public IImageDatabaseService(ServiceCtx context)
        {
            _metadata = DatabaseImpl.Instance.CreateSessionMetadata(new SpecialMiiKeyCode());
        }

        [CommandCmif(0)]
        // Initialize(b8) -> b8
        public ResultCode Initialize(ServiceCtx context)
        {
            // TODO: Service uses MiiImage:/database.dat if true, seems to use hardcoded data if false.
            bool useHardcodedData = context.RequestData.ReadBoolean();

            _imageCount = DatabaseImpl.Instance.GetCount(_metadata, SourceFlag.Database);
            _isDirty = false;

            context.ResponseData.Write(_isDirty);

            Logger.Stub?.PrintStub(LogClass.ServiceMii, new { useHardcodedData, _imageCount });

            return ResultCode.Success;
        }

        [CommandCmif(11)]
        // GetCount() -> u32
        public ResultCode GetCount(ServiceCtx context)
        {
            _imageCount = DatabaseImpl.Instance.GetCount(_metadata, SourceFlag.Database);

            context.ResponseData.Write(_imageCount);

            Logger.Stub?.PrintStub(LogClass.ServiceMii, new { _imageCount });

            return ResultCode.Success;
        }
    }
}

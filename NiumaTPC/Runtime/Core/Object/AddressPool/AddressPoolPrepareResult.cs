using NiumaResource;

namespace NiumaTPC.Core.Object
{
    public readonly struct AddressPoolPrepareResult
    {
        public string Address { get; }
        public bool Succeeded => ErrorCode == AddressPoolErrorCode.None;
        public AddressPoolErrorCode ErrorCode { get; }
        public ResourceLoadErrorCode ResourceErrorCode { get; }
        public string ErrorMessage { get; }

        private AddressPoolPrepareResult(
            string address,
            AddressPoolErrorCode errorCode,
            ResourceLoadErrorCode resourceErrorCode,
            string errorMessage)
        {
            Address = address ?? string.Empty;
            ErrorCode = errorCode;
            ResourceErrorCode = resourceErrorCode;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static AddressPoolPrepareResult Success(string address)
        {
            return new AddressPoolPrepareResult(
                address,
                AddressPoolErrorCode.None,
                ResourceLoadErrorCode.None,
                string.Empty);
        }

        public static AddressPoolPrepareResult Failure(
            string address,
            AddressPoolErrorCode errorCode,
            string errorMessage,
            ResourceLoadErrorCode resourceErrorCode = ResourceLoadErrorCode.None)
        {
            return new AddressPoolPrepareResult(
                address,
                errorCode,
                resourceErrorCode,
                errorMessage);
        }
    }
}
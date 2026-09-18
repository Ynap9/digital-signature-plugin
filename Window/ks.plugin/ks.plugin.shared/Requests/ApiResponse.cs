namespace ks.plugin.shared.Requests
{
    /// <summary>
    /// Vỏ trả về, giữ đúng hình dạng envelope của BE để FE dùng chung một kiểu đọc kết quả cho cả hai nguồn.
    /// </summary>
    public class ApiResponse
    {
        public StatusCodeE Status { get; set; }
        public object? Data { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }

        public ApiResponse(StatusCodeE status, object? data, int code, string message)
        {
            Status = status;
            Data = data;
            Code = code;
            Message = message;
        }

        public ApiResponse(object? data)
        {
            Status = StatusCodeE.Success;
            Data = data;
            Code = 200;
            Message = "Ok";
        }

        public ApiResponse()
        {
            Status = StatusCodeE.Success;
            Data = null;
            Code = 200;
            Message = "Ok";
        }
    }

    public enum StatusCodeE
    {
        Success = 1,
        Error = 0
    }
}

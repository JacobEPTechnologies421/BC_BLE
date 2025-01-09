namespace BoatControl.Shared.Api
{
    public class LoginArgs
    {
        public string Email { get; set; }

        public string Password { get; set; }
    }

    public class CreateUserArgs
    {
        public string Email { get; set; }

        public string FullName { get; set; }

        public string Password { get; set; }
    }

    public class ApiResponse<TModel>
    {
        public TModel Item { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }

        public static ApiResponse<TModel> CreateSuccess(TModel item)
        {
            return new ApiResponse<TModel>()
            {
                Item = item,
                Success = true
            };
        }
        public static ApiResponse<TModel> CreateFailure(string message)
        {
            return new ApiResponse<TModel>()
            {
                Success = false,
                Message = message
            };
        }

    }
}
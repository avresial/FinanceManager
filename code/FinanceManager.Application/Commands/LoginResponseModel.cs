namespace FinanceManager.Api.Models
{
    public class LoginResponseModel
    {
        public required string UserName { get; set; }
        public required string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}

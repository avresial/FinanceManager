namespace FinanceManagerApi.Models
{
    public class LoginRequestModel
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
}

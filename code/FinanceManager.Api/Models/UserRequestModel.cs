namespace FinanceManager.Api.Models
{
    public class UserRequestModel
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
}

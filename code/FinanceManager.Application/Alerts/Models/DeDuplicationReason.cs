namespace FinanceManager.Application.Alerts.Models;

public enum DeDuplicationReason
{
    None = 0,
    UnchangedCondition = 1,
    CooldownActive = 2,
    AlertDisabled = 3
}
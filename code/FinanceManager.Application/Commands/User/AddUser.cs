﻿using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.User;

public record AddUser(string userName, string password, PricingLevel pricingLevel);

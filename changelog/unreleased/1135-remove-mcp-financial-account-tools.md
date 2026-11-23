---
type: Removed
---
Remove the MCP financial account tools (`list_financial_accounts`, `get_financial_account`) and address transactions by kind and id: `list_transactions` filters by kind and currency instead of account, and `get_transaction` takes `kind` and `transactionId`. #1135

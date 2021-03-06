﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevExpress.Mvvm;
using Making.Cents.Common.Enums;
using Making.Cents.Common.Ids;
using Making.Cents.Common.Models;
using Making.Cents.Common.Support;
using Making.Cents.Data.Services;
using Making.Cents.Wpf.Common.Contracts;
using Making.Cents.Wpf.Common.Utilities;
using Making.Cents.Wpf.Common.ViewModels;
using MoreLinq;

namespace Making.Cents.AccountsModule.ViewModels
{
	public class AccountRegisterViewModel : ViewModelBase, IMainWindowTab
	{
		#region Administrative
		private readonly AccountService _accountService;
		private readonly SecurityService _securityService;
		private readonly TransactionService _transactionService;

		public AccountRegisterViewModel(
			AccountService accountService,
			SecurityService securityService,
			TransactionService transactionService)
		{
			_accountService = accountService;
			_securityService = securityService;
			_transactionService = transactionService;
		}

		private bool _isInitialized;


		public void Initialize(AccountId accountId)
		{
			if (_isInitialized)
				return;

			Title = _accountService.GetAccount(accountId).Name;
			Transactions.Replace(
				_transactionService
					.GetTransactionsForAccount(accountId)
					.Append(default)
					.Scan(
						default(TransactionViewModel?),
						(tvm, t) =>
						{
							var ntvm = new TransactionViewModel(accountId, t, tvm?.RunningTotal ?? 0m);
							return ntvm;
						})
					.Choose(t => t is not null ? (true, t) : default));
			_isInitialized = true;
		}

		public string Title { get; private set; } = null!;
		public LoadingViewModel LoadingViewModel { get; } = new();

		public bool CanClose() => false;
		public void Close()
		{
			Transactions.Clear();
			_isInitialized = false;
		}
		#endregion

		public ObservableCollection<TransactionViewModel> Transactions { get; } = new();

		public class TransactionViewModel : ViewModelBase
		{
			private readonly AccountId _registerAccountId;

			public TransactionViewModel(AccountId registerAccountId, Transaction? transaction, decimal runningTotal)
			{
				_registerAccountId = registerAccountId;

				TransactionId = transaction?.TransactionId ?? SequentialGuid.Next();
				Date = transaction?.Date ?? DateTime.Today;
				Description = transaction?.Description ?? string.Empty;
				Memo = transaction?.Memo;

				_items =
					(IReadOnlyList<TransactionItem>?)transaction?.TransactionItems
					?? Array.Empty<TransactionItem>();

				IsSplit = _items.Count > 2;

				if (!IsSplit)
					Account = _items
						.Where(a => a.AccountId != registerAccountId)
						.FirstOrDefault()
						?.Account;

				TransactionTotal = _items
					.Where(i => i.AccountId == registerAccountId)
					.Where(i => i.SecurityId == Security.CashSecurityId)
					.Sum(i => i.Amount);
				RunningTotal = runningTotal + TransactionTotal;
			}

			public TransactionId TransactionId { get; private set; }
			public DateTime Date { get; set; }
			public string Description { get; set; }
			public string? Memo { get; set; }

			public bool IsSplit { get; private set; }
			public Account? Account { get; set; }
			public decimal TransactionTotal { get; set; }

			public decimal RunningTotal { get; set; }

			private IReadOnlyList<TransactionItem> _items;
		}
	}
}

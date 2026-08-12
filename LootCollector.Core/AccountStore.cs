using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LootCollector.Core;

public sealed class AccountStore
{
	private readonly string _path;

	private List<Account> _accounts;

	public AccountStore(string path = null)
	{
		_path = path ?? Paths.AccountsFile;
		_accounts = Load();
	}

	private List<Account> Load()
	{
		try
		{
			if (File.Exists(_path))
			{
				List<Account> list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(_path));
				if (list != null)
				{
					return list;
				}
			}
		}
		catch
		{
		}
		return new List<Account>();
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path));
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		File.WriteAllText(_path, JsonSerializer.Serialize(_accounts, options));
	}

	public IReadOnlyList<Account> All()
	{
		return _accounts;
	}

	public List<string> Usernames()
	{
		return (from a in _accounts
			select a.Username into n
			where !string.IsNullOrEmpty(n)
			select n).ToList();
	}

	public Account Find(string nameOrId)
	{
		if (string.IsNullOrEmpty(nameOrId))
		{
			return null;
		}
		return _accounts.FirstOrDefault((Account a) => string.Equals(a.Username, nameOrId, StringComparison.OrdinalIgnoreCase) || a.UserId.ToString() == nameOrId);
	}

	public void AddOrUpdate(string username, long userId, string cookiePlain)
	{
		Account account = Find(username);
		if (account == null)
		{
			account = new Account();
			_accounts.Add(account);
		}
		account.Username = username;
		account.UserId = userId;
		account.Cookie = Encrypt(cookiePlain);
		Save();
	}

	public bool Remove(string nameOrId)
	{
		Account account = Find(nameOrId);
		if (account == null)
		{
			return false;
		}
		_accounts.Remove(account);
		Save();
		return true;
	}

	public int RemoveMany(IEnumerable<string> namesOrIds)
	{
		HashSet<string> set = new HashSet<string>(namesOrIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		if (set.Count == 0)
		{
			return 0;
		}
		int num = _accounts.RemoveAll((Account a) => set.Contains(a.Username) || set.Contains(a.UserId.ToString()));
		if (num > 0)
		{
			Save();
		}
		return num;
	}

	public string GetCookie(string nameOrId)
	{
		Account account = Find(nameOrId);
		if (account != null)
		{
			return Decrypt(account.Cookie);
		}
		return null;
	}

	private static string Encrypt(string text)
	{
		try
		{
			byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser);
			return "dpapi:" + Convert.ToBase64String(inArray);
		}
		catch
		{
			return "plain:" + text;
		}
	}

	private static string Decrypt(string stored)
	{
		if (string.IsNullOrEmpty(stored))
		{
			return "";
		}
		if (stored.StartsWith("dpapi:"))
		{
			try
			{
				byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(6)), null, DataProtectionScope.CurrentUser);
				return Encoding.UTF8.GetString(bytes);
			}
			catch
			{
				return "";
			}
		}
		if (stored.StartsWith("plain:"))
		{
			return stored.Substring(6);
		}
		return stored;
	}
}

﻿using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using EMS.Witness.Models;
using EMS.Witness.Shared.Toasts;
using System.Net.Http.Json;
using System.Text.Json;
using static Core.Application.CoreApplicationConstants;

namespace EMS.Witness.Services;

public class ApiService : IApiService
{
    private readonly IPermissionsService permissionsService;
    private readonly HttpClient httpClient;
	private readonly ToasterService toasterService;
	private readonly IState context;

	public ApiService(
        IPermissionsService permissionsService,
		HttpClient httpClient,
		ToasterService toasterService,
		IState context)
    {
        this.permissionsService = permissionsService;
        this.httpClient = httpClient;
		this.toasterService = toasterService;
		this.context = context;
	}

	public async Task Handshake()
	{
		try
		{
			if (await this.permissionsService.HasNetworkPermissions())
			{
				var response = await this.httpClient.GetAsync(this.context.ApiHost);
				if (response.IsSuccessStatusCode)
				{
					var toast = new Toast("Handshake succesful", "Established connection to the EMS API", UiColor.Success, 10);
					this.toasterService.Add(toast);
				}
			}
			else
			{
				var toast = new Toast(
					"Network permission rejected",
					"eWitness app cannot operate without Network permissions. Grant permissions in device settings.",
					UiColor.Danger,
					10);
				this.toasterService.Add(toast);
			}
			
		}
		catch (Exception exception)
		{
			this.ToastError(exception);
		}
	}

	public async Task<List<StartModel>> GetStartlist()
    {
        try
        {
			return await this.httpClient.GetFromJsonAsync<List<StartModel>>(this.context.ApiHost + Api.STARTLIST)
				?? new List<StartModel>();
		}
        catch (Exception exception)
        {
			this.ToastError(exception);
			return new List<StartModel>();
        }
    }

	public async Task<bool> PostWitnessEvent(ManualWitnessEvent witnessEvent)
    {
		try
		{
			var test = JsonSerializer.Serialize(witnessEvent);
			var result = await this.httpClient.PostAsJsonAsync(this.context.ApiHost+ Api.WITNESS, witnessEvent);
			if (!result.IsSuccessStatusCode)
			{
				throw new Exception($"Unsuccessful response: {result.StatusCode}");
			}
			return true;
		}
		catch (Exception exception)
		{
			this.ToastError(exception);
			return false;
		}
    }

	private void ToastError(Exception exception)
	{
		var toast = new Toast(exception.Message, exception.StackTrace, UiColor.Danger, 30);
		this.toasterService.Add(toast);
	}
}

public interface IApiService
{
	Task Handshake();
	Task<List<StartModel>> GetStartlist();
	Task<bool> PostWitnessEvent(ManualWitnessEvent witnessEvent);
}

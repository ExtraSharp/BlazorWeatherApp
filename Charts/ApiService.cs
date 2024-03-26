﻿using Charts.Models;
using RestSharp;

namespace Charts;

public class ApiService
{
    private readonly RestClient _client;

    public ApiService()
    {
        const string baseUrl = "https://api.brightsky.dev/";
        _client = new RestClient(baseUrl);
    }

    public async Task<WeatherResponseModel?> GetCurrentWeatherData(string latitude, string longitude)
    {
        var response = await _client.GetJsonAsync<WeatherResponseModel>($"current_weather?lat={latitude}&lon={longitude}");

        return response;
    }

    public async Task<MultipleWeatherResponseModel?> GetHistoricalWeatherData(string date, string latitude, string longitude)
    {
        var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>($"weather?lat={latitude}&lon={longitude}&date={date}");

        return response;
    }

    public async Task<MultipleWeatherResponseModel?> GetMonthlyData(string date, string lastDate, string latitude, string longitude)
    {
        var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>($"weather?lat={latitude}&lon={longitude}&date={date}&last_date={lastDate}");

        return response;
    }
}

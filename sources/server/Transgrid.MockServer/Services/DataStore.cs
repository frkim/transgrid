using Transgrid.MockServer.Models;

namespace Transgrid.MockServer.Services;

public class DataStore
{
    private readonly object _lock = new();
    private readonly List<TrainPlan> _trainPlans = new();
    private readonly List<NegotiatedRate> _negotiatedRates = new();
    private readonly List<CifSchedule> _cifSchedules = new();
    
    // Store baseline data for true reset functionality
    private List<TrainPlan> _baselineTrainPlans = new();
    private List<NegotiatedRate> _baselineNegotiatedRates = new();
    private List<CifSchedule> _baselineCifSchedules = new();

    public DataStore()
    {
        GenerateBaselineData();
        SaveBaseline();
    }

    // Train Plans
    public List<TrainPlan> GetTrainPlans()
    {
        lock (_lock)
        {
            return new List<TrainPlan>(_trainPlans);
        }
    }

    public TrainPlan? GetTrainPlan(string id)
    {
        lock (_lock)
        {
            return _trainPlans.FirstOrDefault(t => t.Id == id);
        }
    }

    public void AddTrainPlan(TrainPlan plan)
    {
        lock (_lock)
        {
            _trainPlans.Add(plan);
        }
    }

    public void UpdateTrainPlan(TrainPlan plan)
    {
        lock (_lock)
        {
            var index = _trainPlans.FindIndex(t => t.Id == plan.Id);
            if (index >= 0)
            {
                _trainPlans[index] = plan;
            }
        }
    }

    public void DeleteTrainPlan(string id)
    {
        lock (_lock)
        {
            _trainPlans.RemoveAll(t => t.Id == id);
        }
    }

    // Negotiated Rates
    public List<NegotiatedRate> GetNegotiatedRates()
    {
        lock (_lock)
        {
            return new List<NegotiatedRate>(_negotiatedRates);
        }
    }

    public NegotiatedRate? GetNegotiatedRate(string id)
    {
        lock (_lock)
        {
            return _negotiatedRates.FirstOrDefault(n => n.Id == id);
        }
    }

    public void AddNegotiatedRate(NegotiatedRate rate)
    {
        lock (_lock)
        {
            _negotiatedRates.Add(rate);
        }
    }

    public void UpdateNegotiatedRate(NegotiatedRate rate)
    {
        lock (_lock)
        {
            var index = _negotiatedRates.FindIndex(n => n.Id == rate.Id);
            if (index >= 0)
            {
                _negotiatedRates[index] = rate;
            }
        }
    }

    public void DeleteNegotiatedRate(string id)
    {
        lock (_lock)
        {
            _negotiatedRates.RemoveAll(n => n.Id == id);
        }
    }

    // CIF Schedules
    public List<CifSchedule> GetCifSchedules()
    {
        lock (_lock)
        {
            return new List<CifSchedule>(_cifSchedules);
        }
    }

    public CifSchedule? GetCifSchedule(string id)
    {
        lock (_lock)
        {
            return _cifSchedules.FirstOrDefault(c => c.Id == id);
        }
    }

    public void AddCifSchedule(CifSchedule schedule)
    {
        lock (_lock)
        {
            _cifSchedules.Add(schedule);
        }
    }

    public void UpdateCifSchedule(CifSchedule schedule)
    {
        lock (_lock)
        {
            var index = _cifSchedules.FindIndex(c => c.Id == schedule.Id);
            if (index >= 0)
            {
                _cifSchedules[index] = schedule;
            }
        }
    }

    public void DeleteCifSchedule(string id)
    {
        lock (_lock)
        {
            _cifSchedules.RemoveAll(c => c.Id == id);
        }
    }

    // Generate New Data
    public void GenerateNewData()
    {
        lock (_lock)
        {
            _trainPlans.Clear();
            _negotiatedRates.Clear();
            _cifSchedules.Clear();
            GenerateBaselineData();
        }
    }

    /// <summary>
    /// Generate deterministic test data with exactly 8 train plans for today's date
    /// that will pass the workflow filters (FR/GB, ACTIVE, not EVOLUTION).
    /// Used for validating the For Each Train Plan loop in workflows.
    /// </summary>
    public void GenerateTestDataForWorkflowValidation()
    {
        lock (_lock)
        {
            _trainPlans.Clear();
            _negotiatedRates.Clear();
            _cifSchedules.Clear();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var dayAfterTomorrow = today.AddDays(2);

            // 8 train plans for TODAY that will PASS the filter (FR/GB, ACTIVE, not EVOLUTION)
            var testPlans = new List<TrainPlan>
            {
                // GB trains - ACTIVE, STANDARD
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9101",
                    Pathway = "UK-FR",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Ebbsfleet International", "Ashford International", "Calais Fréthun" },
                    Origin = "London St Pancras",
                    Destination = "Paris Gare du Nord",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9102",
                    Pathway = "UK-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Ebbsfleet International", "Lille Europe" },
                    Origin = "London St Pancras",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9103",
                    Pathway = "UK-NL",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Ashford International", "Brussels Midi" },
                    Origin = "London St Pancras",
                    Destination = "Amsterdam Centraal",
                    Status = "ACTIVE",
                    PlanType = "ALTERNATIVE",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                // FR trains - ACTIVE, STANDARD
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9201",
                    Pathway = "FR-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Lille Europe" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-6)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9202",
                    Pathway = "FR-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Lille Europe", "Antwerp Central" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Amsterdam Centraal",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9203",
                    Pathway = "UK-FR",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Calais Fréthun", "Ashford International", "Ebbsfleet International" },
                    Origin = "Paris Gare du Nord",
                    Destination = "London St Pancras",
                    Status = "ACTIVE",
                    PlanType = "ALTERNATIVE",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                // Additional GB trains
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9104",
                    Pathway = "UK-FR",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Ebbsfleet International", "Calais Fréthun" },
                    Origin = "London St Pancras",
                    Destination = "Lille Europe",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9204",
                    Pathway = "FR-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Lille Europe" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-8)
                }
            };

            // Add plans that should be EXCLUDED by the filter
            var excludedPlans = new List<TrainPlan>
            {
                // Excluded: Wrong country (BE)
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9301",
                    Pathway = "BE-NL",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Rotterdam Centraal" },
                    Origin = "Brussels Midi",
                    Destination = "Amsterdam Centraal",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "BE",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                // Excluded: Wrong status (CANCELLED)
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9302",
                    Pathway = "UK-FR",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Calais Fréthun" },
                    Origin = "London St Pancras",
                    Destination = "Paris Gare du Nord",
                    Status = "CANCELLED",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                },
                // Excluded: Wrong planType (EVOLUTION)
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9303",
                    Pathway = "UK-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Lille Europe" },
                    Origin = "London St Pancras",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "EVOLUTION",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                // Excluded: DELAYED status
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9304",
                    Pathway = "FR-BE",
                    TravelDate = today,
                    PassagePoints = new List<string> { "Lille Europe" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Brussels Midi",
                    Status = "DELAYED",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            // Plans for D+2 (2 days from now) - for testing rne-d2-export
            var d2Plans = new List<TrainPlan>
            {
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9401",
                    Pathway = "UK-FR",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Ebbsfleet International", "Calais Fréthun" },
                    Origin = "London St Pancras",
                    Destination = "Paris Gare du Nord",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9402",
                    Pathway = "FR-BE",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Lille Europe" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9403",
                    Pathway = "UK-BE",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Ashford International", "Lille Europe" },
                    Origin = "London St Pancras",
                    Destination = "Brussels Midi",
                    Status = "ACTIVE",
                    PlanType = "ALTERNATIVE",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9404",
                    Pathway = "FR-BE",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Lille Europe", "Antwerp Central" },
                    Origin = "Paris Gare du Nord",
                    Destination = "Amsterdam Centraal",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "FR",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9405",
                    Pathway = "UK-NL",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Brussels Midi", "Rotterdam Centraal" },
                    Origin = "London St Pancras",
                    Destination = "Amsterdam Centraal",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new TrainPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceCode = "ES9406",
                    Pathway = "UK-FR",
                    TravelDate = dayAfterTomorrow,
                    PassagePoints = new List<string> { "Ebbsfleet International" },
                    Origin = "London St Pancras",
                    Destination = "Lille Europe",
                    Status = "ACTIVE",
                    PlanType = "STANDARD",
                    Country = "GB",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            // Add all plans
            foreach (var plan in testPlans) _trainPlans.Add(plan);
            foreach (var plan in excludedPlans) _trainPlans.Add(plan);
            foreach (var plan in d2Plans) _trainPlans.Add(plan);

            // Also generate negotiated rates and CIF schedules for completeness
            GenerateNegotiatedRates();
            GenerateCifSchedules();
        }
    }

    // Reset to Baseline - restores the original baseline dataset
    public void ResetToBaseline()
    {
        lock (_lock)
        {
            _trainPlans.Clear();
            _negotiatedRates.Clear();
            _cifSchedules.Clear();
            
            // Deep copy baseline data back to active collections
            foreach (var plan in _baselineTrainPlans)
            {
                _trainPlans.Add(new TrainPlan
                {
                    Id = plan.Id,
                    ServiceCode = plan.ServiceCode,
                    Pathway = plan.Pathway,
                    TravelDate = plan.TravelDate,
                    PassagePoints = new List<string>(plan.PassagePoints),
                    Origin = plan.Origin,
                    Destination = plan.Destination,
                    Status = plan.Status,
                    PlanType = plan.PlanType,
                    Country = plan.Country,
                    CreatedAt = plan.CreatedAt
                });
            }
            
            foreach (var rate in _baselineNegotiatedRates)
            {
                _negotiatedRates.Add(new NegotiatedRate
                {
                    Id = rate.Id,
                    AccountManager = rate.AccountManager,
                    AccountName = rate.AccountName,
                    UniqueCode = rate.UniqueCode,
                    CodeRecordType = rate.CodeRecordType,
                    GdsUsed = rate.GdsUsed,
                    Pcc = rate.Pcc,
                    Distributor = rate.Distributor,
                    Road = rate.Road,
                    TariffCodes = new List<string>(rate.TariffCodes),
                    Discounts = new Dictionary<string, double>(rate.Discounts),
                    Priority = rate.Priority,
                    ActionType = rate.ActionType,
                    ExtractRequested = rate.ExtractRequested,
                    B2bStatus = rate.B2bStatus,
                    B2bExtractDate = rate.B2bExtractDate,
                    CreatedAt = rate.CreatedAt
                });
            }
            
            foreach (var schedule in _baselineCifSchedules)
            {
                _cifSchedules.Add(new CifSchedule
                {
                    Id = schedule.Id,
                    TrainServiceNumber = schedule.TrainServiceNumber,
                    TravelDate = schedule.TravelDate,
                    CifStpIndicator = schedule.CifStpIndicator,
                    ScheduleLocations = schedule.ScheduleLocations.Select(loc => new ScheduleLocation
                    {
                        LocationCode = loc.LocationCode,
                        LocationName = loc.LocationName,
                        ScheduledArrivalTime = loc.ScheduledArrivalTime,
                        ScheduledDepartureTime = loc.ScheduledDepartureTime,
                        Platform = loc.Platform,
                        Activity = loc.Activity
                    }).ToList(),
                    TrainCategory = schedule.TrainCategory,
                    PowerType = schedule.PowerType,
                    TrainClass = schedule.TrainClass,
                    Operator = schedule.Operator,
                    ValidFrom = schedule.ValidFrom,
                    ValidTo = schedule.ValidTo,
                    CreatedAt = schedule.CreatedAt
                });
            }
        }
    }

    // Update Data with realistic changes based on time period
    public void UpdateData(int periodMinutes)
    {
        lock (_lock)
        {
            var random = new Random();
            var now = DateTime.UtcNow;
            const double MaxDiscountChangePercent = 0.05;
            
            // Update Train Plans
            foreach (var plan in _trainPlans)
            {
                // Update travel date forward by the period
                plan.TravelDate = plan.TravelDate.AddMinutes(periodMinutes);
                
                // Simulate status changes based on period
                if (plan.Status == "ACTIVE")
                {
                    // 10% chance of becoming delayed
                    if (random.Next(100) < 10)
                    {
                        plan.Status = "DELAYED";
                    }
                    // 5% chance of being cancelled
                    else if (random.Next(100) < 5)
                    {
                        plan.Status = "CANCELLED";
                    }
                }
                else if (plan.Status == "DELAYED")
                {
                    // 20% chance delayed trains become active again
                    if (random.Next(100) < 20)
                    {
                        plan.Status = "ACTIVE";
                    }
                }
            }
            
            // Update Negotiated Rates
            foreach (var rate in _negotiatedRates)
            {
                // Update extract status progression
                if (rate.B2bStatus == "Pending" && random.Next(100) < 30)
                {
                    rate.B2bStatus = "Extracted";
                    rate.B2bExtractDate = now;
                }
                else if (rate.B2bStatus == "Pending" && random.Next(100) < 5)
                {
                    rate.B2bStatus = "Failed";
                }
                
                // Toggle extract requested flag occasionally
                if (random.Next(100) < 15)
                {
                    rate.ExtractRequested = !rate.ExtractRequested;
                }
                
                // Update discounts slightly (±5%)
                var tariffKeys = rate.Discounts.Keys.ToList();
                foreach (var key in tariffKeys)
                {
                    if (random.Next(100) < 20)
                    {
                        var change = (random.NextDouble() * 2 * MaxDiscountChangePercent) - MaxDiscountChangePercent;
                        rate.Discounts[key] = Math.Max(0, Math.Min(100, rate.Discounts[key] + change));
                    }
                }
            }
            
            // Update CIF Schedules
            foreach (var schedule in _cifSchedules)
            {
                // Update travel dates forward by the period
                schedule.TravelDate = schedule.TravelDate.AddMinutes(periodMinutes);
                schedule.ValidFrom = schedule.ValidFrom.AddMinutes(periodMinutes);
                schedule.ValidTo = schedule.ValidTo.AddMinutes(periodMinutes);
                
                // Update schedule times for locations (add minutes to times)
                foreach (var location in schedule.ScheduleLocations)
                {
                    if (!string.IsNullOrEmpty(location.ScheduledArrivalTime))
                    {
                        location.ScheduledArrivalTime = UpdateTimeString(location.ScheduledArrivalTime, periodMinutes);
                    }
                    if (!string.IsNullOrEmpty(location.ScheduledDepartureTime))
                    {
                        location.ScheduledDepartureTime = UpdateTimeString(location.ScheduledDepartureTime, periodMinutes);
                    }
                    
                    // Occasionally change platform
                    if (random.Next(100) < 10)
                    {
                        var platforms = new[] { "1", "2", "3", "4", "5", "6" };
                        location.Platform = platforms[random.Next(platforms.Length)];
                    }
                }
            }
        }
    }
    
    private string UpdateTimeString(string timeString, int minutesToAdd)
    {
        // Parse time string (format: HH:mm)
        if (TimeSpan.TryParse(timeString, out var time))
        {
            var totalMinutes = (int)((time.TotalMinutes + minutesToAdd) % (24 * 60));
            if (totalMinutes < 0) totalMinutes += 24 * 60;
            return TimeSpan.FromMinutes(totalMinutes).ToString(@"hh\:mm");
        }
        return timeString;
    }
    
    private void SaveBaseline()
    {
        lock (_lock)
        {
            _baselineTrainPlans = new List<TrainPlan>(_trainPlans);
            _baselineNegotiatedRates = new List<NegotiatedRate>(_negotiatedRates);
            _baselineCifSchedules = new List<CifSchedule>(_cifSchedules);
        }
    }

    private void GenerateBaselineData()
    {
        GenerateTrainPlans();
        GenerateNegotiatedRates();
        GenerateCifSchedules();
    }

    private void GenerateTrainPlans()
    {
        var random = new Random();
        var stations = new[] { "London St Pancras", "Paris Gare du Nord", "Brussels Midi", "Amsterdam Centraal", "Lille Europe", "Calais Fréthun", "Ebbsfleet International", "Ashford International" };
        var pathways = new[] { "UK-FR", "UK-BE", "UK-NL", "FR-BE", "BE-NL" };
        var countries = new[] { "GB", "FR", "BE", "NL" };
        var statuses = new[] { "ACTIVE", "CANCELLED", "DELAYED" };
        var planTypes = new[] { "STANDARD", "EVOLUTION", "ALTERNATIVE" };

        for (int i = 0; i < 25; i++)
        {
            var origin = stations[random.Next(stations.Length)];
            var destination = stations.Where(s => s != origin).OrderBy(_ => random.Next()).First();
            var passageCount = random.Next(2, 5);
            var passagePoints = stations.Where(s => s != origin && s != destination)
                .OrderBy(_ => random.Next())
                .Take(passageCount)
                .ToList();

            _trainPlans.Add(new TrainPlan
            {
                ServiceCode = $"ES{9000 + i}",
                Pathway = pathways[random.Next(pathways.Length)],
                TravelDate = DateTime.Today.AddDays(random.Next(-7, 30)),
                PassagePoints = passagePoints,
                Origin = origin,
                Destination = destination,
                Status = statuses[random.Next(statuses.Length)],
                PlanType = planTypes[random.Next(planTypes.Length)],
                Country = countries[random.Next(countries.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 90))
            });
        }
    }

    private void GenerateNegotiatedRates()
    {
        var random = new Random();
        var accountManagers = new[] { "John Smith", "Sarah Johnson", "Michael Brown", "Emma Davis", "James Wilson", "Emily Taylor", "David Anderson", "Sophie Martin" };
        var companies = new[] { "Global Travel Corp", "Business Express Ltd", "Corporate Solutions Inc", "TravelPro Services", "Elite Business Travel", "Premier Travel Group", "Executive Journeys", "Metro Travel Partners" };
        var codeTypes = new[] { "GND BE", "GND NL", "FCE (New)", "Corporate code Amadeus", "Apollo", "Galileo", "Sabre" };
        var gdsOptions = new[] { "Amadeus", "Apollo", "Galileo", "Sabre" };
        var distributors = new[] { "TravelDist NL", "BeneluxTravel", "EuroDistrib", "TransportHub" };
        var roads = new[] { "London-Paris", "London-Brussels", "Paris-Amsterdam", "Brussels-London" };
        var tariffCodes = new[] { "STDR", "FLEX", "BSNS", "FRST", "PREM", "ECON" };
        var priorities = new[] { "Normal", "Priority" };
        var actions = new[] { "CREATE", "UPDATE", "DELETE" };
        var statuses = new[] { "Pending", "Extracted", "Failed" };

        for (int i = 0; i < 30; i++)
        {
            var codeType = codeTypes[random.Next(codeTypes.Length)];
            var isGds = codeType.Contains("Amadeus") || codeType.Contains("Apollo") || codeType.Contains("Galileo") || codeType.Contains("Sabre");
            var isBene = codeType == "GND BE" || codeType == "GND NL";

            var tariffList = Enumerable.Range(0, random.Next(2, 5))
                .Select(_ => tariffCodes[random.Next(tariffCodes.Length)])
                .Distinct()
                .ToList();

            var discounts = new Dictionary<string, double>();
            foreach (var tariff in tariffList)
            {
                discounts[tariff] = Math.Round(random.NextDouble() * 30 + 5, 2); // 5-35% discount
            }

            _negotiatedRates.Add(new NegotiatedRate
            {
                AccountManager = accountManagers[random.Next(accountManagers.Length)],
                AccountName = companies[random.Next(companies.Length)],
                UniqueCode = $"NC{10000 + i}",
                CodeRecordType = codeType,
                GdsUsed = isGds ? gdsOptions[random.Next(gdsOptions.Length)] : string.Empty,
                Pcc = isGds ? $"LON{random.Next(1000, 9999)}" : string.Empty,
                Distributor = isBene ? distributors[random.Next(distributors.Length)] : string.Empty,
                Road = roads[random.Next(roads.Length)],
                TariffCodes = tariffList,
                Discounts = discounts,
                Priority = priorities[random.Next(priorities.Length)],
                ActionType = actions[random.Next(actions.Length)],
                ExtractRequested = random.Next(2) == 0,
                B2bStatus = statuses[random.Next(statuses.Length)],
                B2bExtractDate = random.Next(2) == 0 ? DateTime.UtcNow.AddDays(-random.Next(1, 30)) : null,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 90))
            });
        }
    }

    private void GenerateCifSchedules()
    {
        var random = new Random();
        var operators = new[] { "Network Rail", "Starline International", "Southern Railway", "Northern Trains" };
        var trainCategories = new[] { "XX", "OO", "XC", "BR" };
        var powerTypes = new[] { "EMU", "DMU", "HST" };
        var trainClasses = new[] { "Standard", "First", "Business" };
        var stations = new[]
        {
            ("STPANCI", "London St Pancras International"),
            ("EBFLEET", "Ebbsfleet International"),
            ("ASHFINT", "Ashford International"),
            ("CLFRETH", "Calais Fréthun"),
            ("LILEUR", "Lille Europe"),
            ("PARGARN", "Paris Gare du Nord"),
            ("BRUMIDI", "Brussels Midi"),
            ("AMSTCEN", "Amsterdam Centraal")
        };
        var activities = new[] { "TB", "T", "D", "U", "R" };

        for (int i = 0; i < 20; i++)
        {
            var numLocations = random.Next(3, 7);
            var selectedStations = stations.OrderBy(_ => random.Next()).Take(numLocations).ToList();
            var scheduleLocations = new List<ScheduleLocation>();
            var baseTime = new DateTime(2026, 1, 1, 6, 0, 0).AddHours(random.Next(0, 16));

            for (int j = 0; j < selectedStations.Count; j++)
            {
                var (code, name) = selectedStations[j];
                var arrivalTime = j == 0 ? "" : baseTime.AddMinutes(j * 45.0).ToString("HH:mm");
                var departureTime = j == selectedStations.Count - 1 ? "" : baseTime.AddMinutes(j * 45.0 + 2).ToString("HH:mm");

                scheduleLocations.Add(new ScheduleLocation
                {
                    LocationCode = code,
                    LocationName = name,
                    ScheduledArrivalTime = arrivalTime,
                    ScheduledDepartureTime = departureTime,
                    Platform = $"{random.Next(1, 12)}",
                    Activity = activities[random.Next(activities.Length)]
                });
            }

            var validFrom = DateTime.Today.AddDays(-random.Next(30, 180));
            _cifSchedules.Add(new CifSchedule
            {
                TrainServiceNumber = $"2X{(50 + i):D2}",
                TravelDate = DateTime.Today.AddDays(random.Next(-7, 30)),
                CifStpIndicator = "N",
                ScheduleLocations = scheduleLocations,
                TrainCategory = trainCategories[random.Next(trainCategories.Length)],
                PowerType = powerTypes[random.Next(powerTypes.Length)],
                TrainClass = trainClasses[random.Next(trainClasses.Length)],
                Operator = operators[random.Next(operators.Length)],
                ValidFrom = validFrom,
                ValidTo = validFrom.AddDays(random.Next(180, 365)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 90))
            });
        }
    }
}

using MDD4All.Github.DataModels;
using MDD4All.DevOpsObserver.DataModels;
using MDD4All.DevOpsObserver.DataProviders.Contracts;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MDD4All.DevOpsObserver.DataProviders.Github
{
    public class GithubStatusProvider : IDevOpsStatusProvider
    {
        private IConfiguration _configuration;
        private HttpClient _httpClient;

        public GithubStatusProvider(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<List<DevOpsStatusInformation>> GetDevOpsStatusListAsync(DevOpsSystem devOpsSystem)
        {
            List<DevOpsStatusInformation> result = new List<DevOpsStatusInformation>();

            foreach (DevOpsObservable devOpsObservable in devOpsSystem.ObservedAutomations)
            {
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = new HttpMethod("GET"),
                    RequestUri = new Uri(devOpsSystem.ServerURL + "/repos/" + devOpsSystem.Tenant + "/" + devOpsObservable.RepositoryName +
                                        "/actions/runs"),
                };

                //string clientID = _configuration[devOpsSystem.GUID + ":LoginName"];
                string clientSecret = _configuration[devOpsSystem.GUID + ":Password"];

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                                                                                                      clientSecret);

                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                request.Headers.Add("User-Agent", "DevOpsObserver");

                try
                {
                    HttpResponseMessage response = await _httpClient.SendAsync(request);
                    HttpStatusCode responseStatusCode = response.StatusCode;

                    if (responseStatusCode == HttpStatusCode.OK)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        WorkflowRunResponse workflowRunResponse = JsonConvert.DeserializeObject<WorkflowRunResponse>(responseBody);

                        if (workflowRunResponse != null && workflowRunResponse.TotalCount > 0)
                        {
                            List<DevOpsStatusInformation> devOpsStatusInformation = ConvertGithubResponseToStatus(workflowRunResponse);

                            foreach (DevOpsStatusInformation devOpsStatus in devOpsStatusInformation)
                            {
                                devOpsStatus.Alias = devOpsObservable.Alias;
                                result.Add(devOpsStatus);
                            }
                        }
                        else
                        {
                            CreateUnknowStateData(result, devOpsObservable);
                        }

                    }
                    else
                    {
                        CreateUnknowStateData(result, devOpsObservable);
                    }
                }
                catch(Exception exception)
                {
                    CreateUnknowStateData(result, devOpsObservable);
                }
            }

            return result;
        }

        private void CreateUnknowStateData(List<DevOpsStatusInformation> result, DevOpsObservable devOpsObservable)
        {
            DevOpsStatusInformation devOpsStatusInformation = new DevOpsStatusInformation
            {
                RepositoryName = devOpsObservable.RepositoryName,
                Branch = devOpsObservable.RepositoryBranch,
                Alias = devOpsObservable.Alias,
                GitServerType = "Github",
            };
            result.Add(devOpsStatusInformation);
        }

        private List<DevOpsStatusInformation> ConvertGithubResponseToStatus(WorkflowRunResponse workflowRunResponse)
        {
            List<DevOpsStatusInformation> result = new List<DevOpsStatusInformation>();

            Dictionary<string, WorkflowRun> workflowRuns = new Dictionary<string, WorkflowRun>();

            for (int counter = 0; counter < workflowRunResponse.WorkflowRuns.Count; counter++)
            {
                WorkflowRun workflowRun = workflowRunResponse.WorkflowRuns[counter];

                if(workflowRun != null)
                {
                    if(!workflowRuns.ContainsKey(workflowRun.WorkflowId.ToString()))
                    {
                        workflowRuns.Add(workflowRun.WorkflowId.ToString(), workflowRun);
                    }
                }
            }

            foreach (KeyValuePair<string, WorkflowRun> keyValuePair in workflowRuns)
            {
                WorkflowRun run = keyValuePair.Value;

                DevOpsStatusInformation statusInformation = new DevOpsStatusInformation
                {
                    GitServerType = "Github",
                    RepositoryName = run.Repository.FullName,
                    ShortName = run.Repository.Name,
                    Branch = run.HeadBranch,
                    BuildNumber = run.RunNumber,
                    BuildTime = run.CreatedAt,
                    WorkflowTitle = run.Name,
                    ID = "github_" + run.WorkflowId,
                };


                if(run.Status == "completed" && run.Conclusion == "success")
                {
                    statusInformation.StatusValue = DevOpsStatus.Success;
                }
                else if(run.Status == "completed" && run.Conclusion == "failure")
                {
                    statusInformation.StatusValue = DevOpsStatus.Fail;
                }

                result.Add(statusInformation);
            }

            return result;
        }
    }
}

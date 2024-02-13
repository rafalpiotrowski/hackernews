# hackernews best stories

This repository contains simple api to extract ```X``` number of best stories ordered by score in descending order.

## Requirements
- .NET 8.0

## Running

```bash
git clone https://github.com/rafalpiotrowski/hackernews.git
cd hackernews
dotnet run --project src/HackerNews/HackerNews.csproj 
```

you might need to run the following command to trust development ssl certificates

```bash
dotnet dev-certs https --trust
```

in another terminal window run the following command

```bash
curl -k -X 'GET' 'https://localhost:7223/2' -H 'accept: text/plain' | json_pp
```

you should see the following example output

```
 ~  curl -k -X 'GET' 'https://localhost:7223/2' -H 'accept: text/plain' | json_pp
  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current
                                 Dload  Upload   Total   Spent    Left  Speed
100   438    0   438    0     0   2358      0 --:--:-- --:--:-- --:--:--  2367
[
   {
      "commentCount" : 95,
      "postedBy" : "dutchkiwifruit",
      "score" : 2427,
      "time" : "2024-02-11T16:36:58+00:00",
      "title" : "I designed a cube that balances itself on a corner",
      "uri" : "https://willempennings.nl/balancing-cube/"
   },
   {
      "commentCount" : 38,
      "postedBy" : "jgrahamc",
      "score" : 1044,
      "time" : "2024-02-12T14:14:42+00:00",
      "title" : "Cloudflare defeats patent troll Sable at trial",
      "uri" : "https://blog.cloudflare.com/cloudflare-defeats-patent-troll-sable-at-trial"
   }
]
```

## API

In the browser window paste this url: https://localhost:7223/swagger to view [Swagger API](https://localhost:7223/swagger) 
and see available api end points.

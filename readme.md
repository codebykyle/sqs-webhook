# SQS Webhook

This project polls an SQS queue for items, and sends the body of the message as a web request to a URL specified in the configuration.

Combined with a lambda  function, which pushes items into the SQS queue, this can be a tool used to synchronize remote, internet facing messages
to a local computer for processing, or moving information between isolated compute environments.


## Usage

TBD.


## Envrionment Variables

| Variable              | Required | Description                                                                                                                                                                                 | Default Value             |
|-----------------------|:--------:|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:--------------------------|
| SQS_ACCESS_KEY_ID     |    ✔     | A AWS IAM API Access Key ID with privileges to access the queue                                                                                                                             |                           |
| SQS_SECRET_ACCESS_KEY |    ✔     | The secret key for the related IAM user                                                                                                                                                     |                           |
| SQS_QUEUE_URL         |    ✔     | The URL of the SQS Queue. You can find this on the Overview Page on AWS                                                                                                                     |                           |
| HTTP_URL              |    ✔     | An HTTP address to send the request                                                                                                                                                         |                           |
| HTTP_METHOD           |    ❌     | The HTTP verb used when sending the request to the HTTP_URL. GET, PUT, POST, etc                                                                                                            | POST                      |
| ERROR_URL             |    ❌     | If the script runs into a non-200 status code, it will send a POST request to this URL. Leaving this blank will disable this feature. See the "Error Handling" section for more information |                           |
| POLL_DELAY            |    ❌     | How long to pause the application between retrieving SQS messages in milliseconds                                                                                                           | 60000                     |
| MAX_MESSAGES          |    ❌     | The maximum number of requests to retrieve from SQS per connection                                                                                                                          | 5                         |
| APP_NAME              |    ❌     | The name of the application. This is used in logging and is sent to the ERROR_URL if there is an issue with the request                                                                     | SQS to HTTP               |
| HEADER_FILE           |    ❌     | Relative or absolute path of a json file containing a dictionary of headers to use while making the request                                                                                 | config/headers.json       |
| HEADER_ERROR_FILE     |    ❌     | Relative or absolute path of a json file containing a dictionary of headers to use while making an error report                                                                             | config/headers_error.json |
| DELETE_ERRORS         |    ❌     | If this is set to true, this will remove messages which received an HTTP error from the queue. If you wish to use a dead-letter queue, set this to False                                    | True                      |


# Error Handling
If you wish to process errors outside of this application, you can set up a dead-letter queue on the SQS page. If `DELETE_ERRORS` is set to False, messages which error will retry, but never be removed from the queue.
If you have your SQS settings configured to support a dead-letter queue, messages will automatically fall into that bucket after the configured delay.

You may also want to receive a notification for when a webhook receives a non-200 response in order to detect potential issues with this script. You can set the `ERROR_URL` envrionment variable, which will send a POST request
to that URL, along with some information about the request and response.

The body of the POST request contains a JSON object, which contains the raw request and response, as well as the request time in a millisecond timestamp:

```json
{
  "ApplicationName": "SQS to HTTP",
  "Result": {
    "Url": "https://postman-echo.com/put",
    "RequestTime": 1645269350720,
    "ResponseTime": 1645269351012,
    "StatusCode": 404,
    "RequestBody": "{\u0022test\u0022:\u0022hello!\u0022}",
    "ResponseBody": "",
    "IsSuccess": false
  }
}
```

If the `DELETE_ERRORS` environment variable is set to `False`, the application will retry the object until it falls off the SQS Queue.
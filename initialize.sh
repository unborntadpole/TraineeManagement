export DOMAIN_OWNER=824100177805
export REGION=ap-south-1
export NUGET_REPO_NAME=training-nuget-store
export CODEARTIFACT_AUTH_TOKEN=`aws codeartifact get-authorization-token --domain zeuslearning --domain-owner $DOMAIN_OWNER --region $REGION --query authorizationToken --output text`

docker compose up -d --build
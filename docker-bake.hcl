variable "TAG_RELEASE" {
  default = "latest"
}

variable "TAG_LATEST" {
  default = "latest"
}

variable "TAG_LATEST_BRANCH" {
  default = "latest"
}

target "test" {
  dockerfile = "src/Podiumd.Adapter/Dockerfile"
  tags       = ["test"]
  cache-from = ["type=gha,scope=cache"]
  cache-to   = ["type=gha,mode=max,scope=cache"]
  output     = ["testresults"]
}

target "web" {
  dockerfile = "src/Podiumd.Adapter/Dockerfile"
  target     = "web"
  tags       = ["${TAG_RELEASE}", "${TAG_LATEST}", "${TAG_LATEST_BRANCH}"]
  cache-from = ["type=gha,scope=cache"]
}
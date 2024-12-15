# typed: false
# frozen_string_literal: true

# This file was generated by https://github.com/magnusopera/terrabuild/blob/main/.github/scripts/generate-homebrew-tap
class Terrabuild < Formula
    desc "Seamless CI/CD tool for building and deploying monorepos."
    homepage "https://terrabuild.io"
    version "${TERRABUILD_VERSION}"
  
    if OS.mac? && Hardware::CPU.intel?
      url "${TERRABUILD_DARWIN_X64_URL}"
      sha256 "${TERRABUILD_DARWIN_X64_SHA256}"
    end
  
    if OS.mac? && Hardware::CPU.arm?
      url "${TERRABUILD_DARWIN_ARM64_URL}"
      sha256 "${TERRABUILD_DARWIN_ARM64_SHA256}"
    end
  
    if OS.linux? && Hardware::CPU.intel?
      url "${TERRABUILD_LINUX_X64_URL}"
      sha256 "${TERRABUILD_LINUX_X64_SHA256}"
    end
  
    if OS.linux? && Hardware::CPU.arm? && Hardware::CPU.is_64_bit?
      url "${TERRABUILD_LINUX_ARM64_URL}"
      sha256 "${TERRABUILD_LINUX_ARM64_SHA256}"
    end

    def install
      bin.install "terrabuild"
    end

    test do
      system "#{bin}/terrabuild version"
    end
  end

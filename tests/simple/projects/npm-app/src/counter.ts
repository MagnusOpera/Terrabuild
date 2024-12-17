import { log } from 'npm-lib'

export function setupCounter(element: HTMLButtonElement) {
  let counter = 0
  log(`${counter}`)
  const setCounter = (count: number) => {
    counter = count
    element.innerHTML = `count is ${counter}`
  }
  element.addEventListener('click', () => setCounter(counter + 1))
  setCounter(0)
}

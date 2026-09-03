import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from '../App'

describe('App', () => {
  it('renders the codedotnet brand and loading status', () => {
    render(<App />)
    expect(screen.getByText('codedotnet')).toBeInTheDocument()
    expect(screen.getByText('Loading development environment...')).toBeInTheDocument()
  })
})
